package main

import (
	"database/sql"
	"fmt"
	"strings"

	_ "modernc.org/sqlite" // 纯 Go 驱动：免 CGO，Windows 无 gcc 也能编译（确认书决策 7）
)

// Store 存储层：只负责 SQLite 行 ↔ Marker 结构体映射，不做 HTTP/校验逻辑
type Store struct {
	DB *sql.DB
}

// OpenDB 打开（不存在则创建）SQLite 单文件并建表。
// 为什么单文件：确认书 Global Constraints 6 —— 重启不丢数据且便于交付拷贝。
func OpenDB(path string) (*sql.DB, error) {
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, err
	}
	// 契约第 3 节 DDL：字段平铺，lat/lng/photo_url 可 NULL（geo/photoUrl 可空）
	_, err = db.Exec(`
CREATE TABLE IF NOT EXISTS markers (
	id          TEXT PRIMARY KEY,
	type        TEXT NOT NULL,
	title       TEXT NOT NULL,
	description TEXT NOT NULL DEFAULT '',
	pos_x REAL NOT NULL, pos_y REAL NOT NULL, pos_z REAL NOT NULL,
	rot_x REAL NOT NULL, rot_y REAL NOT NULL, rot_z REAL NOT NULL, rot_w REAL NOT NULL,
	lat REAL, lng REAL,
	status      TEXT NOT NULL DEFAULT 'pending',
	reporter    TEXT NOT NULL,
	photo_url   TEXT,
	created_at  TEXT NOT NULL,
	updated_at  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_markers_status ON markers(status);
CREATE INDEX IF NOT EXISTS idx_markers_type ON markers(type);`)
	if err != nil {
		return nil, fmt.Errorf("migrate: %w", err)
	}
	return db, nil
}

// scanRow 把一行记录映射为 Marker；sql.Row 与 sql.Rows 都满足 scanner 接口。
// 为什么单独抽函数：Get/List 共用同一映射逻辑，避免列序写两遍出错。
type scanner interface{ Scan(dest ...interface{}) error }

func scanRow(row scanner) (Marker, error) {
	var m Marker
	var lat, lng sql.NullFloat64 // NULL → geo 为 nil（契约：geo 可空）
	var photo sql.NullString     // NULL → photoUrl 为 null
	err := row.Scan(
		&m.ID, &m.Type, &m.Title, &m.Description,
		&m.Position.X, &m.Position.Y, &m.Position.Z,
		&m.Rotation.X, &m.Rotation.Y, &m.Rotation.Z, &m.Rotation.W,
		&lat, &lng, &m.Status, &m.Reporter, &photo,
		&m.CreatedAt, &m.UpdatedAt,
	)
	if err != nil {
		return Marker{}, err
	}
	if lat.Valid && lng.Valid {
		m.Geo = &Geo{Lat: lat.Float64, Lng: lng.Float64}
	}
	if photo.Valid {
		p := photo.String
		m.PhotoURL = &p
	}
	return m, nil
}

// Insert 写入新标注。nil 指针字段以 nil 参数写入 → SQL NULL，保持可空语义。
func (s *Store) Insert(m *Marker) error {
	var lat, lng interface{}
	if m.Geo != nil {
		lat, lng = m.Geo.Lat, m.Geo.Lng
	}
	var photo interface{}
	if m.PhotoURL != nil {
		photo = *m.PhotoURL
	}
	_, err := s.DB.Exec(`
INSERT INTO markers
(id, type, title, description,
 pos_x, pos_y, pos_z,
 rot_x, rot_y, rot_z, rot_w,
 lat, lng, status, reporter, photo_url,
 created_at, updated_at)
VALUES (?,?,?,?, ?,?,?, ?,?,?,?, ?,?,?,?,?, ?,?)`,
		m.ID, m.Type, m.Title, m.Description,
		m.Position.X, m.Position.Y, m.Position.Z,
		m.Rotation.X, m.Rotation.Y, m.Rotation.Z, m.Rotation.W,
		lat, lng, m.Status, m.Reporter, photo,
		m.CreatedAt, m.UpdatedAt)
	return err
}

// Get 按 id 查询；无记录返回 sql.ErrNoRows（handler 据此映射 40401）
func (s *Store) Get(id string) (Marker, error) {
	row := s.DB.QueryRow(`
SELECT id,type,title,description,
       pos_x,pos_y,pos_z,
       rot_x,rot_y,rot_z,rot_w,
       lat,lng,status,reporter,photo_url,
       created_at,updated_at
FROM markers WHERE id = ?`, id)
	return scanRow(row)
}

// List 分页 + 可选过滤（status/type）。
// 为什么 WHERE 1=1：动态拼接 AND 条件时避免"首个条件前要不要 WHERE"的分支。
// 返回 (items, total, err)：total 供 React 分页器计算页数。
func (s *Store) List(status, typ string, page, pageSize int) ([]Marker, int, error) {
	cond := " WHERE 1=1"
	args := []interface{}{}
	if status != "" {
		cond += " AND status = ?"
		args = append(args, status)
	}
	if typ != "" {
		cond += " AND type = ?"
		args = append(args, typ)
	}

	var total int
	if err := s.DB.QueryRow("SELECT COUNT(*) FROM markers"+cond, args...).Scan(&total); err != nil {
		return nil, 0, err
	}

	// 分页：page 从 1 开始，offset=(page-1)*pageSize；按创建时间倒序（最新在前）
	rows, err := s.DB.Query(`
SELECT id,type,title,description,
       pos_x,pos_y,pos_z,
       rot_x,rot_y,rot_z,rot_w,
       lat,lng,status,reporter,photo_url,
       created_at,updated_at
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

// Update 部分更新：fields 只含 handler 白名单过滤后的键（status/title/description），
// 动态 SET + 参数化（防注入），并重写 updated_at。返回更新后的完整记录。
func (s *Store) Update(id string, fields map[string]interface{}) (Marker, error) {
	// 字段名→列名映射：二次防御，未知键直接忽略（handler 已过滤，这里兜底）
	colMap := map[string]string{
		"status":      "status",
		"title":       "title",
		"description": "description",
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
		// 无字段可更新（如空 body 或全是未知键）：直接返回现状
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
