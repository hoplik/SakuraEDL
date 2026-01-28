// MultiFlash Admin Panel - Backend API Server
// 后台管理面板 - Go API 服务器
package main

import (
	"crypto/md5"
	"database/sql"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	_ "modernc.org/sqlite"
)

// ==================== 数据模型 ====================

// Loader 模型
type Loader struct {
	ID          int64     `json:"id"`
	Filename    string    `json:"filename"`
	Vendor      string    `json:"vendor"`
	Chip        string    `json:"chip"`
	HwID        string    `json:"hw_id"`
	PkHash      string    `json:"pk_hash"`
	OemID       string    `json:"oem_id"`
	AuthType    string    `json:"auth_type"`    // none, miauth, demacia, vip
	StorageType string    `json:"storage_type"` // ufs, emmc
	FileSize    int64     `json:"file_size"`
	FileMD5     string    `json:"file_md5"`
	FilePath    string    `json:"-"` // 内部使用，不返回给前端
	DigestPath  string    `json:"-"` // VIP 验证：digest 文件路径
	SignPath    string    `json:"-"` // VIP 验证：sign 文件路径
	HasDigest   bool      `json:"has_digest"`
	HasSign     bool      `json:"has_sign"`
	IsEnabled   bool      `json:"is_enabled"`
	Downloads   int64     `json:"downloads"`
	MatchCount  int64     `json:"match_count"`
	Notes       string    `json:"notes"`
	CreatedAt   time.Time `json:"created_at"`
	UpdatedAt   time.Time `json:"updated_at"`
}

// DeviceLog 设备日志
type DeviceLog struct {
	ID          int64     `json:"id"`
	Platform    string    `json:"platform"`
	MsmID       string    `json:"msm_id"`
	PkHash      string    `json:"pk_hash"`
	OemID       string    `json:"oem_id"`
	StorageType string    `json:"storage_type"`
	MatchResult string    `json:"match_result"`
	LoaderID    *int64    `json:"loader_id"`
	ClientIP    string    `json:"client_ip"`
	UserAgent   string    `json:"user_agent"`
	CreatedAt   time.Time `json:"created_at"`
}

// API 响应
type Response struct {
	Code    int         `json:"code"`
	Message string      `json:"message"`
	Data    interface{} `json:"data,omitempty"`
}

// ==================== 全局变量 ====================

var db *sql.DB
var uploadDir = "./uploads"

// ==================== 主函数 ====================

func main() {
	// 初始化日志
	log.SetFlags(log.LstdFlags | log.Lshortfile)

	// 初始化数据库
	initDatabase()

	// 确保上传目录存在
	os.MkdirAll(filepath.Join(uploadDir, "loaders"), 0755)
	os.MkdirAll(filepath.Join(uploadDir, "digest"), 0755)
	os.MkdirAll(filepath.Join(uploadDir, "sign"), 0755)

	// 设置路由
	mux := http.NewServeMux()

	// 公开 API (客户端使用)
	mux.HandleFunc("/api/loaders/list", corsMiddleware(handleLoaderList))
	mux.HandleFunc("/api/loaders/match", corsMiddleware(handleMatch))
	mux.HandleFunc("/api/loaders/", corsMiddleware(handleLoaderDownload))
	mux.HandleFunc("/api/device-logs", corsMiddleware(handleDeviceLog))

	// 管理 API (需要认证)
	mux.HandleFunc("/api/admin/loaders", corsMiddleware(authMiddleware(handleAdminLoaders)))
	mux.HandleFunc("/api/admin/loaders/upload", corsMiddleware(authMiddleware(handleUpload)))
	mux.HandleFunc("/api/admin/loaders/", corsMiddleware(authMiddleware(handleAdminLoaderAction)))
	mux.HandleFunc("/api/admin/stats", corsMiddleware(authMiddleware(handleStats)))
	mux.HandleFunc("/api/admin/logs", corsMiddleware(authMiddleware(handleAdminLogs)))
	mux.HandleFunc("/api/admin/login", corsMiddleware(handleLogin))

	// 静态文件服务 (前端)
	mux.Handle("/", http.FileServer(http.Dir("./static")))

	port := ":8082"
	log.Printf("🚀 MultiFlash Admin API 服务器启动于 http://localhost%s", port)
	log.Printf("📁 上传目录: %s", uploadDir)
	log.Fatal(http.ListenAndServe(port, mux))
}

// ==================== 数据库初始化 ====================

func initDatabase() {
	var err error
	db, err = sql.Open("sqlite", "./multiflash.db")
	if err != nil {
		log.Fatal("数据库连接失败:", err)
	}

	// 创建 loaders 表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS loaders (
			id INTEGER PRIMARY KEY AUTOINCREMENT,
			filename TEXT NOT NULL,
			vendor TEXT DEFAULT '',
			chip TEXT DEFAULT '',
			hw_id TEXT DEFAULT '',
			pk_hash TEXT DEFAULT '',
			oem_id TEXT DEFAULT '',
			auth_type TEXT DEFAULT 'none',
			storage_type TEXT DEFAULT 'ufs',
			file_size INTEGER DEFAULT 0,
			file_md5 TEXT DEFAULT '',
			file_path TEXT DEFAULT '',
			digest_path TEXT DEFAULT '',
			sign_path TEXT DEFAULT '',
			is_enabled INTEGER DEFAULT 1,
			downloads INTEGER DEFAULT 0,
			match_count INTEGER DEFAULT 0,
			notes TEXT DEFAULT '',
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
		)
	`)
	if err != nil {
		log.Fatal("创建 loaders 表失败:", err)
	}

	// 创建 device_logs 表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS device_logs (
			id INTEGER PRIMARY KEY AUTOINCREMENT,
			platform TEXT DEFAULT 'qualcomm',
			msm_id TEXT DEFAULT '',
			pk_hash TEXT DEFAULT '',
			oem_id TEXT DEFAULT '',
			storage_type TEXT DEFAULT '',
			match_result TEXT DEFAULT '',
			loader_id INTEGER,
			client_ip TEXT DEFAULT '',
			user_agent TEXT DEFAULT '',
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP
		)
	`)
	if err != nil {
		log.Fatal("创建 device_logs 表失败:", err)
	}

	// 创建索引
	db.Exec(`CREATE INDEX IF NOT EXISTS idx_loaders_hw_id ON loaders(hw_id)`)
	db.Exec(`CREATE INDEX IF NOT EXISTS idx_loaders_pk_hash ON loaders(pk_hash)`)
	db.Exec(`CREATE INDEX IF NOT EXISTS idx_loaders_chip ON loaders(chip)`)
	db.Exec(`CREATE INDEX IF NOT EXISTS idx_device_logs_msm_id ON device_logs(msm_id)`)
	db.Exec(`CREATE INDEX IF NOT EXISTS idx_device_logs_created_at ON device_logs(created_at)`)

	log.Println("✅ 数据库初始化完成")
}

// ==================== 中间件 ====================

func corsMiddleware(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Access-Control-Allow-Origin", "*")
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Admin-Token")

		if r.Method == "OPTIONS" {
			w.WriteHeader(http.StatusOK)
			return
		}

		next(w, r)
	}
}

func authMiddleware(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		// 简单的 Token 验证 (生产环境应使用 JWT)
		token := r.Header.Get("X-Admin-Token")
		if token == "" {
			token = r.URL.Query().Get("token")
		}

		// 默认管理员 Token (生产环境应从配置读取)
		validToken := os.Getenv("ADMIN_TOKEN")
		if validToken == "" {
			validToken = "multiflash-admin-2024"
		}

		if token != validToken {
			sendJSON(w, http.StatusUnauthorized, Response{
				Code:    401,
				Message: "未授权访问",
			})
			return
		}

		next(w, r)
	}
}

// ==================== 公开 API 处理器 ====================

// 获取 Loader 列表 (公开接口，供客户端选择)
func handleLoaderList(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	// 可选筛选参数
	storageType := r.URL.Query().Get("storage_type")
	vendor := r.URL.Query().Get("vendor")

	// 构建查询
	where := "is_enabled = 1"
	args := []interface{}{}

	if storageType != "" {
		where += " AND storage_type = ?"
		args = append(args, storageType)
	}
	if vendor != "" {
		where += " AND vendor LIKE ?"
		args = append(args, "%"+vendor+"%")
	}

	rows, err := db.Query(`
		SELECT id, filename, vendor, chip, hw_id, auth_type, storage_type, file_size
		FROM loaders WHERE `+where+` ORDER BY vendor, chip, filename
	`, args...)

	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	loaders := []map[string]interface{}{}
	for rows.Next() {
		var id, fileSize int64
		var filename, vendor, chip, hwID, authType, storageType string

		err := rows.Scan(&id, &filename, &vendor, &chip, &hwID, &authType, &storageType, &fileSize)
		if err != nil {
			continue
		}

		loaders = append(loaders, map[string]interface{}{
			"id":           id,
			"filename":     filename,
			"vendor":       vendor,
			"chip":         chip,
			"hw_id":        hwID,
			"auth_type":    authType,
			"storage_type": storageType,
			"file_size":    fileSize,
			// 显示名称: 厂商 - 芯片 - 文件名
			"display_name": fmt.Sprintf("[%s] %s - %s", vendor, chip, filename),
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"loaders": loaders,
			"count":   len(loaders),
		},
	})
}

// 匹配 Loader
func handleMatch(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var req struct {
		MsmID       string `json:"msm_id"`
		PkHash      string `json:"pk_hash"`
		OemID       string `json:"oem_id"`
		StorageType string `json:"storage_type"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	// 匹配优先级：pk_hash > hw_id > chip
	var loader Loader
	var found bool

	// 1. 精确匹配 pk_hash
	if req.PkHash != "" {
		row := db.QueryRow(`
			SELECT id, filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
			       file_size, file_md5, file_path, digest_path, sign_path
			FROM loaders 
			WHERE pk_hash = ? AND is_enabled = 1
			LIMIT 1
		`, req.PkHash)
		if err := scanLoader(row, &loader); err == nil {
			found = true
		}
	}

	// 2. 匹配 hw_id (MSM ID)
	if !found && req.MsmID != "" {
		row := db.QueryRow(`
			SELECT id, filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
			       file_size, file_md5, file_path, digest_path, sign_path
			FROM loaders 
			WHERE hw_id = ? AND is_enabled = 1
			LIMIT 1
		`, req.MsmID)
		if err := scanLoader(row, &loader); err == nil {
			found = true
		}
	}

	if !found {
		sendJSON(w, http.StatusOK, Response{
			Code:    404,
			Message: "未找到匹配的 Loader",
		})
		return
	}

	// 更新匹配计数
	db.Exec("UPDATE loaders SET match_count = match_count + 1 WHERE id = ?", loader.ID)

	// 记录设备日志
	go logDevice(req.MsmID, req.PkHash, req.OemID, req.StorageType, "matched", &loader.ID, r)

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "匹配成功",
		Data: map[string]interface{}{
			"loader": map[string]interface{}{
				"id":           loader.ID,
				"filename":     loader.Filename,
				"vendor":       loader.Vendor,
				"chip":         loader.Chip,
				"hw_id":        loader.HwID,
				"auth_type":    loader.AuthType,
				"storage_type": loader.StorageType,
			},
			"match_type": getMatchType(req.PkHash, loader.PkHash, req.MsmID, loader.HwID),
			"score":      getMatchScore(req.PkHash, loader.PkHash, req.MsmID, loader.HwID),
		},
	})
}

// 下载 Loader / Digest / Sign
func handleLoaderDownload(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	// 解析 URL: /api/loaders/{id}/download 或 /api/loaders/{id}/digest 或 /api/loaders/{id}/sign
	path := strings.TrimPrefix(r.URL.Path, "/api/loaders/")
	
	// 排除已被其他路由处理的路径
	if path == "list" || path == "match" {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的请求路径"})
		return
	}
	
	parts := strings.Split(path, "/")
	if len(parts) < 2 {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的请求路径"})
		return
	}
	
	action := parts[1]
	if action != "download" && action != "digest" && action != "sign" {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的请求路径，支持: download, digest, sign"})
		return
	}

	id, err := strconv.ParseInt(parts[0], 10, 64)
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的 Loader ID"})
		return
	}

	// 查询 Loader
	var loader Loader
	row := db.QueryRow(`
		SELECT id, filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
		       file_size, file_md5, file_path, digest_path, sign_path
		FROM loaders WHERE id = ? AND is_enabled = 1
	`, id)

	if err := scanLoader(row, &loader); err != nil {
		sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: "Loader 不存在"})
		return
	}

	var filePath, fileName string
	switch action {
	case "download":
		filePath = loader.FilePath
		fileName = loader.Filename
		// 更新下载计数
		db.Exec("UPDATE loaders SET downloads = downloads + 1 WHERE id = ?", id)
	case "digest":
		filePath = loader.DigestPath
		fileName = strings.TrimSuffix(loader.Filename, filepath.Ext(loader.Filename)) + "_digest.bin"
	case "sign":
		filePath = loader.SignPath
		fileName = strings.TrimSuffix(loader.Filename, filepath.Ext(loader.Filename)) + "_sign.bin"
	}

	// 检查文件是否存在
	if filePath == "" {
		sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: fmt.Sprintf("%s 文件未配置", action)})
		return
	}
	if _, err := os.Stat(filePath); os.IsNotExist(err) {
		sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: fmt.Sprintf("%s 文件不存在", action)})
		return
	}

	// 返回文件
	w.Header().Set("Content-Disposition", fmt.Sprintf("attachment; filename=%s", fileName))
	w.Header().Set("Content-Type", "application/octet-stream")
	http.ServeFile(w, r, filePath)
}

// 设备日志上报
func handleDeviceLog(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var req struct {
		Platform    string `json:"platform"`
		MsmID       string `json:"msm_id"`
		PkHash      string `json:"pk_hash"`
		OemID       string `json:"oem_id"`
		StorageType string `json:"storage_type"`
		MatchResult string `json:"match_result"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	go logDevice(req.MsmID, req.PkHash, req.OemID, req.StorageType, req.MatchResult, nil, r)

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "日志已记录"})
}

// ==================== 管理 API 处理器 ====================

// 登录
func handleLogin(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var req struct {
		Username string `json:"username"`
		Password string `json:"password"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	// 简单验证 (生产环境应使用数据库)
	adminUser := os.Getenv("ADMIN_USER")
	adminPass := os.Getenv("ADMIN_PASS")
	if adminUser == "" {
		adminUser = "admin"
	}
	if adminPass == "" {
		adminPass = "multiflash2024"
	}

	if req.Username != adminUser || req.Password != adminPass {
		sendJSON(w, http.StatusUnauthorized, Response{Code: 401, Message: "用户名或密码错误"})
		return
	}

	token := os.Getenv("ADMIN_TOKEN")
	if token == "" {
		token = "multiflash-admin-2024"
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "登录成功",
		Data: map[string]interface{}{
			"token":    token,
			"username": req.Username,
		},
	})
}

// Loader 列表
func handleAdminLoaders(w http.ResponseWriter, r *http.Request) {
	switch r.Method {
	case "GET":
		// 获取列表
		page, _ := strconv.Atoi(r.URL.Query().Get("page"))
		pageSize, _ := strconv.Atoi(r.URL.Query().Get("page_size"))
		keyword := r.URL.Query().Get("keyword")
		authType := r.URL.Query().Get("auth_type")

		if page < 1 {
			page = 1
		}
		if pageSize < 1 || pageSize > 100 {
			pageSize = 20
		}

		// 构建查询
		where := "1=1"
		args := []interface{}{}

		if keyword != "" {
			where += " AND (filename LIKE ? OR vendor LIKE ? OR chip LIKE ? OR hw_id LIKE ?)"
			kw := "%" + keyword + "%"
			args = append(args, kw, kw, kw, kw)
		}
		if authType != "" {
			where += " AND auth_type = ?"
			args = append(args, authType)
		}

		// 获取总数
		var total int64
		db.QueryRow("SELECT COUNT(*) FROM loaders WHERE "+where, args...).Scan(&total)

		// 获取列表
		args = append(args, pageSize, (page-1)*pageSize)
		rows, err := db.Query(`
			SELECT id, filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
			       file_size, file_md5, digest_path, sign_path, is_enabled, downloads, match_count,
			       notes, created_at, updated_at
			FROM loaders WHERE `+where+` ORDER BY id DESC LIMIT ? OFFSET ?
		`, args...)

		if err != nil {
			sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
			return
		}
		defer rows.Close()

		loaders := []Loader{}
		for rows.Next() {
			var l Loader
			var digestPath, signPath string
			var isEnabled int
			var createdAt, updatedAt string

			err := rows.Scan(
				&l.ID, &l.Filename, &l.Vendor, &l.Chip, &l.HwID, &l.PkHash, &l.OemID,
				&l.AuthType, &l.StorageType, &l.FileSize, &l.FileMD5, &digestPath, &signPath,
				&isEnabled, &l.Downloads, &l.MatchCount, &l.Notes, &createdAt, &updatedAt,
			)
			if err != nil {
				continue
			}

			l.IsEnabled = isEnabled == 1
			l.HasDigest = digestPath != ""
			l.HasSign = signPath != ""
			l.CreatedAt, _ = time.Parse("2006-01-02 15:04:05", createdAt)
			l.UpdatedAt, _ = time.Parse("2006-01-02 15:04:05", updatedAt)

			loaders = append(loaders, l)
		}

		sendJSON(w, http.StatusOK, Response{
			Code:    0,
			Message: "获取成功",
			Data: map[string]interface{}{
				"list":      loaders,
				"total":     total,
				"page":      page,
				"page_size": pageSize,
			},
		})

	default:
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
	}
}

// 上传 Loader
func handleUpload(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	// 解析 multipart form (最大 100MB)
	if err := r.ParseMultipartForm(100 << 20); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求解析失败: " + err.Error()})
		return
	}

	// 获取主 loader 文件
	loaderFile, loaderHeader, err := r.FormFile("loader")
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "缺少 loader 文件"})
		return
	}
	defer loaderFile.Close()

	// 获取元数据
	vendor := r.FormValue("vendor")
	chip := r.FormValue("chip")
	hwID := r.FormValue("hw_id")
	pkHash := r.FormValue("pk_hash")
	oemID := r.FormValue("oem_id")
	authType := r.FormValue("auth_type")
	storageType := r.FormValue("storage_type")
	notes := r.FormValue("notes")

	if authType == "" {
		authType = "none"
	}
	if storageType == "" {
		storageType = "ufs"
	}

	// 验证 auth_type
	validAuthTypes := map[string]bool{"none": true, "miauth": true, "demacia": true, "vip": true}
	if !validAuthTypes[authType] {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的验证类型"})
		return
	}

	// VIP 类型需要 digest 和 sign 文件
	var digestPath, signPath string
	if authType == "vip" {
		digestFile, digestHeader, err := r.FormFile("digest")
		if err != nil {
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "VIP 类型需要上传 digest 文件"})
			return
		}
		defer digestFile.Close()

		signFile, signHeader, err := r.FormFile("sign")
		if err != nil {
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "VIP 类型需要上传 sign 文件"})
			return
		}
		defer signFile.Close()

		// 保存 digest 文件
		digestPath, err = saveUploadedFile(digestFile, digestHeader.Filename, "digest")
		if err != nil {
			sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "保存 digest 文件失败"})
			return
		}

		// 保存 sign 文件
		signPath, err = saveUploadedFile(signFile, signHeader.Filename, "sign")
		if err != nil {
			sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "保存 sign 文件失败"})
			return
		}
	}

	// 保存 loader 文件
	loaderPath, err := saveUploadedFile(loaderFile, loaderHeader.Filename, "loaders")
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "保存 loader 文件失败"})
		return
	}

	// 计算文件大小和 MD5
	fileInfo, _ := os.Stat(loaderPath)
	fileSize := fileInfo.Size()

	fileData, _ := os.ReadFile(loaderPath)
	fileMD5 := md5.Sum(fileData)
	fileMD5Str := hex.EncodeToString(fileMD5[:])

	// 插入数据库
	result, err := db.Exec(`
		INSERT INTO loaders (filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
		                     file_size, file_md5, file_path, digest_path, sign_path, notes)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
	`, loaderHeader.Filename, vendor, chip, hwID, pkHash, oemID, authType, storageType,
		fileSize, fileMD5Str, loaderPath, digestPath, signPath, notes)

	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "保存到数据库失败: " + err.Error()})
		return
	}

	id, _ := result.LastInsertId()

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "上传成功",
		Data: map[string]interface{}{
			"id":        id,
			"filename":  loaderHeader.Filename,
			"file_size": fileSize,
			"file_md5":  fileMD5Str,
			"auth_type": authType,
		},
	})
}

// Loader 操作 (更新、删除、启用/禁用)
func handleAdminLoaderAction(w http.ResponseWriter, r *http.Request) {
	// 解析 ID
	path := strings.TrimPrefix(r.URL.Path, "/api/admin/loaders/")
	parts := strings.Split(path, "/")
	if len(parts) < 1 || parts[0] == "" {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的请求路径"})
		return
	}

	id, err := strconv.ParseInt(parts[0], 10, 64)
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的 Loader ID"})
		return
	}

	action := ""
	if len(parts) > 1 {
		action = parts[1]
	}

	switch r.Method {
	case "GET":
		// 获取单个 Loader 详情
		var l Loader
		row := db.QueryRow(`
			SELECT id, filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
			       file_size, file_md5, file_path, digest_path, sign_path, is_enabled, downloads,
			       match_count, notes, created_at, updated_at
			FROM loaders WHERE id = ?
		`, id)

		var digestPath, signPath string
		var isEnabled int
		var createdAt, updatedAt string

		err := row.Scan(
			&l.ID, &l.Filename, &l.Vendor, &l.Chip, &l.HwID, &l.PkHash, &l.OemID,
			&l.AuthType, &l.StorageType, &l.FileSize, &l.FileMD5, &l.FilePath,
			&digestPath, &signPath, &isEnabled, &l.Downloads, &l.MatchCount, &l.Notes,
			&createdAt, &updatedAt,
		)
		if err != nil {
			sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: "Loader 不存在"})
			return
		}

		l.IsEnabled = isEnabled == 1
		l.HasDigest = digestPath != ""
		l.HasSign = signPath != ""
		l.CreatedAt, _ = time.Parse("2006-01-02 15:04:05", createdAt)
		l.UpdatedAt, _ = time.Parse("2006-01-02 15:04:05", updatedAt)

		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "获取成功", Data: l})

	case "PUT":
		// 更新 Loader
		var req struct {
			Vendor      string `json:"vendor"`
			Chip        string `json:"chip"`
			HwID        string `json:"hw_id"`
			PkHash      string `json:"pk_hash"`
			OemID       string `json:"oem_id"`
			AuthType    string `json:"auth_type"`
			StorageType string `json:"storage_type"`
			Notes       string `json:"notes"`
			IsEnabled   *bool  `json:"is_enabled"`
		}

		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
			return
		}

		// 构建更新语句
		updates := []string{}
		args := []interface{}{}

		if req.Vendor != "" {
			updates = append(updates, "vendor = ?")
			args = append(args, req.Vendor)
		}
		if req.Chip != "" {
			updates = append(updates, "chip = ?")
			args = append(args, req.Chip)
		}
		if req.HwID != "" {
			updates = append(updates, "hw_id = ?")
			args = append(args, req.HwID)
		}
		if req.PkHash != "" {
			updates = append(updates, "pk_hash = ?")
			args = append(args, req.PkHash)
		}
		if req.OemID != "" {
			updates = append(updates, "oem_id = ?")
			args = append(args, req.OemID)
		}
		if req.AuthType != "" {
			updates = append(updates, "auth_type = ?")
			args = append(args, req.AuthType)
		}
		if req.StorageType != "" {
			updates = append(updates, "storage_type = ?")
			args = append(args, req.StorageType)
		}
		if req.Notes != "" {
			updates = append(updates, "notes = ?")
			args = append(args, req.Notes)
		}
		if req.IsEnabled != nil {
			enabled := 0
			if *req.IsEnabled {
				enabled = 1
			}
			updates = append(updates, "is_enabled = ?")
			args = append(args, enabled)
		}

		if len(updates) == 0 {
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "没有要更新的字段"})
			return
		}

		updates = append(updates, "updated_at = CURRENT_TIMESTAMP")
		args = append(args, id)

		_, err := db.Exec("UPDATE loaders SET "+strings.Join(updates, ", ")+" WHERE id = ?", args...)
		if err != nil {
			sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "更新失败"})
			return
		}

		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "更新成功"})

	case "DELETE":
		// 删除 Loader
		// 先获取文件路径
		var filePath, digestPath, signPath string
		db.QueryRow("SELECT file_path, digest_path, sign_path FROM loaders WHERE id = ?", id).Scan(&filePath, &digestPath, &signPath)

		// 删除数据库记录
		_, err := db.Exec("DELETE FROM loaders WHERE id = ?", id)
		if err != nil {
			sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "删除失败"})
			return
		}

		// 删除文件
		if filePath != "" {
			os.Remove(filePath)
		}
		if digestPath != "" {
			os.Remove(digestPath)
		}
		if signPath != "" {
			os.Remove(signPath)
		}

		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "删除成功"})

	case "POST":
		// 特殊操作
		switch action {
		case "enable":
			db.Exec("UPDATE loaders SET is_enabled = 1, updated_at = CURRENT_TIMESTAMP WHERE id = ?", id)
			sendJSON(w, http.StatusOK, Response{Code: 0, Message: "已启用"})
		case "disable":
			db.Exec("UPDATE loaders SET is_enabled = 0, updated_at = CURRENT_TIMESTAMP WHERE id = ?", id)
			sendJSON(w, http.StatusOK, Response{Code: 0, Message: "已禁用"})
		default:
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "未知操作"})
		}

	default:
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
	}
}

// 统计数据
func handleStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	stats := make(map[string]interface{})

	// Loader 统计
	var totalLoaders, enabledLoaders, totalDownloads, totalMatches int64
	db.QueryRow("SELECT COUNT(*) FROM loaders").Scan(&totalLoaders)
	db.QueryRow("SELECT COUNT(*) FROM loaders WHERE is_enabled = 1").Scan(&enabledLoaders)
	db.QueryRow("SELECT COALESCE(SUM(downloads), 0) FROM loaders").Scan(&totalDownloads)
	db.QueryRow("SELECT COALESCE(SUM(match_count), 0) FROM loaders").Scan(&totalMatches)
	stats["total_loaders"] = totalLoaders
	stats["enabled_loaders"] = enabledLoaders
	stats["total_downloads"] = totalDownloads
	stats["total_matches"] = totalMatches

	// 按验证类型统计
	authStats := make(map[string]int64)
	rows, _ := db.Query("SELECT auth_type, COUNT(*) FROM loaders GROUP BY auth_type")
	for rows.Next() {
		var authType string
		var count int64
		rows.Scan(&authType, &count)
		authStats[authType] = count
	}
	rows.Close()
	stats["auth_type_stats"] = authStats

	// 按厂商统计
	vendorStats := make(map[string]int64)
	rows, _ = db.Query("SELECT vendor, COUNT(*) FROM loaders WHERE vendor != '' GROUP BY vendor")
	for rows.Next() {
		var vendor string
		var count int64
		rows.Scan(&vendor, &count)
		vendorStats[vendor] = count
	}
	rows.Close()
	stats["vendor_stats"] = vendorStats

	// 设备日志统计
	var totalLogs, logsToday int64
	db.QueryRow("SELECT COUNT(*) FROM device_logs").Scan(&totalLogs)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE created_at > datetime('now', '-1 day')").Scan(&logsToday)
	stats["total_logs"] = totalLogs
	stats["logs_today"] = logsToday

	// 最近匹配的设备
	recentDevices := []map[string]interface{}{}
	rows, _ = db.Query(`
		SELECT msm_id, pk_hash, storage_type, match_result, created_at 
		FROM device_logs ORDER BY id DESC LIMIT 10
	`)
	for rows.Next() {
		var msmID, pkHash, storageType, matchResult, createdAt string
		rows.Scan(&msmID, &pkHash, &storageType, &matchResult, &createdAt)
		recentDevices = append(recentDevices, map[string]interface{}{
			"msm_id":       msmID,
			"pk_hash":      pkHash,
			"storage_type": storageType,
			"match_result": matchResult,
			"created_at":   createdAt,
		})
	}
	rows.Close()
	stats["recent_devices"] = recentDevices

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "获取成功", Data: stats})
}

// 设备日志列表
func handleAdminLogs(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	page, _ := strconv.Atoi(r.URL.Query().Get("page"))
	pageSize, _ := strconv.Atoi(r.URL.Query().Get("page_size"))
	keyword := r.URL.Query().Get("keyword")
	resultFilter := r.URL.Query().Get("result")

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 200 {
		pageSize = 50
	}

	// 构建查询条件
	where := "1=1"
	args := []interface{}{}

	if keyword != "" {
		where += " AND (msm_id LIKE ? OR pk_hash LIKE ?)"
		args = append(args, "%"+keyword+"%", "%"+keyword+"%")
	}
	if resultFilter != "" {
		where += " AND match_result = ?"
		args = append(args, resultFilter)
	}

	// 获取总数
	var total int64
	countArgs := args
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE "+where, countArgs...).Scan(&total)

	// 获取统计数据
	stats := map[string]int64{}
	var matched, notFound, failed, today int64
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'matched'").Scan(&matched)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'not_found'").Scan(&notFound)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'failed'").Scan(&failed)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE created_at > datetime('now', '-1 day')").Scan(&today)
	stats["matched"] = matched
	stats["not_found"] = notFound
	stats["failed"] = failed
	stats["today"] = today

	// 获取日志列表
	queryArgs := append(args, pageSize, (page-1)*pageSize)
	rows, err := db.Query(`
		SELECT id, platform, msm_id, pk_hash, oem_id, storage_type, match_result, 
		       loader_id, client_ip, user_agent, created_at
		FROM device_logs WHERE `+where+` ORDER BY id DESC LIMIT ? OFFSET ?
	`, queryArgs...)

	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	logs := []DeviceLog{}
	for rows.Next() {
		var l DeviceLog
		var loaderID sql.NullInt64
		var createdAt string

		err := rows.Scan(&l.ID, &l.Platform, &l.MsmID, &l.PkHash, &l.OemID, &l.StorageType,
			&l.MatchResult, &loaderID, &l.ClientIP, &l.UserAgent, &createdAt)
		if err != nil {
			continue
		}

		if loaderID.Valid {
			l.LoaderID = &loaderID.Int64
		}
		l.CreatedAt, _ = time.Parse("2006-01-02 15:04:05", createdAt)

		logs = append(logs, l)
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"list":      logs,
			"total":     total,
			"page":      page,
			"page_size": pageSize,
			"stats":     stats,
		},
	})
}

// ==================== 辅助函数 ====================

func sendJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

func saveUploadedFile(file io.Reader, filename, subdir string) (string, error) {
	// 生成唯一文件名
	timestamp := time.Now().UnixNano()
	newFilename := fmt.Sprintf("%d_%s", timestamp, filename)

	// 保存路径
	savePath := filepath.Join(uploadDir, subdir, newFilename)

	// 创建目标文件
	dst, err := os.Create(savePath)
	if err != nil {
		return "", err
	}
	defer dst.Close()

	// 复制文件内容
	if _, err := io.Copy(dst, file); err != nil {
		return "", err
	}

	return savePath, nil
}

func scanLoader(row *sql.Row, l *Loader) error {
	return row.Scan(
		&l.ID, &l.Filename, &l.Vendor, &l.Chip, &l.HwID, &l.PkHash, &l.OemID,
		&l.AuthType, &l.StorageType, &l.FileSize, &l.FileMD5, &l.FilePath,
		&l.DigestPath, &l.SignPath,
	)
}

func logDevice(msmID, pkHash, oemID, storageType, matchResult string, loaderID *int64, r *http.Request) {
	clientIP := r.RemoteAddr
	if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
		clientIP = strings.Split(xff, ",")[0]
	}
	userAgent := r.UserAgent()

	db.Exec(`
		INSERT INTO device_logs (msm_id, pk_hash, oem_id, storage_type, match_result, loader_id, client_ip, user_agent)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)
	`, msmID, pkHash, oemID, storageType, matchResult, loaderID, clientIP, userAgent)
}

func getMatchType(reqPkHash, loaderPkHash, reqMsmID, loaderHwID string) string {
	if reqPkHash != "" && reqPkHash == loaderPkHash {
		return "exact_pk_hash"
	}
	if reqMsmID != "" && reqMsmID == loaderHwID {
		return "hw_id"
	}
	return "fuzzy"
}

func getMatchScore(reqPkHash, loaderPkHash, reqMsmID, loaderHwID string) int {
	if reqPkHash != "" && reqPkHash == loaderPkHash {
		return 100
	}
	if reqMsmID != "" && reqMsmID == loaderHwID {
		return 80
	}
	return 50
}
