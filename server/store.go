package main

import (
	"crypto/sha256"
	"database/sql"
	"encoding/hex"
	"fmt"
	"strings"

	_ "modernc.org/sqlite" // 纯 Go 驱动：免 CGO，Windows 无 gcc 也能编译
)

// Store 存储层：只负责 SQLite 行 ↔ Marker 结构体映射，不做 HTTP/校验逻辑
type Store struct {
	DB *sql.DB
}

// OpenDB 打开（不存在则创建）SQLite 单文件并建表。
// v2.0 对齐任务书：priority 字段、status 三态、position 仅 x/y/z、dedup_hash 防重放。
func OpenDB(path string) (*sql.DB, error) {
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, err
	}
	_, err = db.Exec(`
CREATE TABLE IF NOT EXISTS markers (
	id          TEXT PRIMARY KEY,
	title       TEXT NOT NULL,
	description TEXT NOT NULL,
	priority    TEXT NOT NULL,
	pos_x REAL NOT NULL, pos_y REAL NOT NULL, pos_z REAL NOT NULL,
	status      TEXT NOT NULL DEFAULT 'open',
	dedup_hash  TEXT NOT NULL UNIQUE,
	created_at  TEXT NOT NULL,
	updated_at  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_markers_status ON markers(status);
CREATE INDEX IF NOT EXISTS idx_markers_priority ON markers(priority);`)
	if err != nil {
		return nil, fmt.Errorf("migrate: %w", err)
	}
	return db, nil
}

// dedupHash 计算防重放哈希（任务书 3.3）：title+description+position 三者组合。
// 为什么含 position：同一位置同一描述才算重复；不同位置同标题是不同问题。
func dedupHash(m *Marker) string {
	h := sha256.Sum256([]byte(fmt.Sprintf("%s|%s|%.4f,%.4f,%.4f",
		m.Title, m.Description, m.Position.X, m.Position.Y, m.Position.Z)))
	return hex.EncodeToString(h[:])
}

// scanRow 把一行记录映射为 Marker；sql.Row 与 sql.Rows 都满足 scanner 接口。
type scanner interface{ Scan(dest ...interface{}) error }

func scanRow(row scanner) (Marker, error) {
	var m Marker
	err := row.Scan(
		&m.ID, &m.Title, &m.Description, &m.Priority,
		&m.Position.X, &m.Position.Y, &m.Position.Z,
		&m.Status, &m.CreatedAt, &m.UpdatedAt,
	)
	if err != nil {
		return Marker{}, err
	}
	return m, nil
}

// ErrDuplicate 防重放：重复上报时返回此错误（handler 映射 409）
var ErrDuplicate = fmt.Errorf("duplicate marker")

// Insert 写入新标记。dedup_hash 唯一索引冲突 → 返回 ErrDuplicate。
func (s *Store) Insert(m *Marker) error {
	_, err := s.DB.Exec(`
INSERT INTO markers
(id, title, description, priority,
 pos_x, pos_y, pos_z,
 status, dedup_hash, created_at, updated_at)
VALUES (?,?,?,?, ?,?,?, ?,?,?,?)`,
		m.ID, m.Title, m.Description, m.Priority,
		m.Position.X, m.Position.Y, m.Position.Z,
		m.Status, dedupHash(m), m.CreatedAt, m.UpdatedAt)
	if err != nil {
		// SQLite UNIQUE 约束冲突 = 防重放命中
		if strings.Contains(err.Error(), "UNIQUE") || strings.Contains(err.Error(), "constraint failed") {
			return ErrDuplicate
		}
		return err
	}
	return nil
}

// Get 按 id 查询；无记录返回 sql.ErrNoRows（handler 据此映射 40401）
func (s *Store) Get(id string) (Marker, error) {
	row := s.DB.QueryRow(`
SELECT id,title,description,priority,
       pos_x,pos_y,pos_z,
       status,created_at,updated_at
FROM markers WHERE id = ?`, id)
	return scanRow(row)
}

// List 分页 + 可选过滤（status/priority）。
// 返回 (items, total, err)：total 供 React 分页器计算页数。
func (s *Store) List(status, priority string, page, pageSize int) ([]Marker, int, error) {
	cond := " WHERE 1=1"
	args := []interface{}{}
	if status != "" {
		cond += " AND status = ?"
		args = append(args, status)
	}
	if priority != "" {
		cond += " AND priority = ?"
		args = append(args, priority)
	}

	var total int
	if err := s.DB.QueryRow("SELECT COUNT(*) FROM markers"+cond, args...).Scan(&total); err != nil {
		return nil, 0, err
	}

	rows, err := s.DB.Query(`
SELECT id,title,description,priority,
       pos_x,pos_y,pos_z,
       status,created_at,updated_at
FROM markers`+cond+` ORDER BY created_at DESC LIMIT ? OFFSET ?`,
		append(args, pageSize, (page-1)*pageSize)...)
	if err != nil {
		return nil, 0, err
	}
	defer rows.Close()

	items := []Marker{}
	for rows.Next() {
		m, err := scanRow(rows)
		if err != nil {
			return nil, 0, err
		}
		items = append(items, m)
	}
	return items, total, rows.Err()
}

// Update 部分更新：fields 只含 handler 白名单过滤后的键（status/title/description/priority），
// 动态 SET + 参数化（防注入），并重写 updated_at。
func (s *Store) Update(id string, fields map[string]interface{}) (Marker, error) {
	colMap := map[string]string{
		"status":      "status",
		"title":       "title",
		"description": "description",
		"priority":    "priority",
	}
	setCols := []string{}
	args := []interface{}{}
	for k, v := range fields {
		col, ok := colMap[k]
		if !ok {
			continue
		}
		setCols = append(setCols, col+" = ?")
		args = append(args, v)
	}
	if len(setCols) == 0 {
		return s.Get(id)
	}
	setCols = append(setCols, "updated_at = ?")
	args = append(args, nowRFC3339(), id)

	_, err := s.DB.Exec("UPDATE markers SET "+strings.Join(setCols, ", ")+" WHERE id = ?", args...)
	if err != nil {
		return Marker{}, err
	}
	return s.Get(id)
}

// Delete 按 id 删除；0 行受影响说明不存在 → sql.ErrNoRows（handler 映射 40401）
func (s *Store) Delete(id string) error {
	res, err := s.DB.Exec("DELETE FROM markers WHERE id = ?", id)
	if err != nil {
		return err
	}
	if n, _ := res.RowsAffected(); n == 0 {
		return sql.ErrNoRows
	}
	return nil
}
