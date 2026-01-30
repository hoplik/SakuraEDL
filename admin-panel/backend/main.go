// SakuraEDL Admin Panel - Backend API Server
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

	_ "github.com/go-sql-driver/mysql"
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
	ID            int64     `json:"id"`
	Platform      string    `json:"platform"`
	SaharaVersion int       `json:"sahara_version"` // Sahara 协议版本 (1/2/3)
	MsmID         string    `json:"msm_id"`
	PkHash        string    `json:"pk_hash"`
	OemID         string    `json:"oem_id"`
	ModelID       string    `json:"model_id"`
	HwID          string    `json:"hw_id"`          // 完整 HWID
	SerialNumber  string    `json:"serial_number"`
	ChipName      string    `json:"chip_name"`      // 芯片名称 (如 SM8550)
	Vendor        string    `json:"vendor"`         // 厂商 (如 Xiaomi, OnePlus)
	StorageType   string    `json:"storage_type"`
	MatchResult   string    `json:"match_result"`
	LoaderID      *int64    `json:"loader_id"`
	ClientIP      string    `json:"client_ip"`
	UserAgent     string    `json:"user_agent"`
	CreatedAt     time.Time `json:"created_at"`
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
	os.MkdirAll(filepath.Join(uploadDir, "mtk"), 0755)
	os.MkdirAll(filepath.Join(uploadDir, "spd"), 0755)

	// 设置路由
	mux := http.NewServeMux()

	// 公开 API (客户端使用)
	mux.HandleFunc("/api/loaders/list", corsMiddleware(handleLoaderList))
	mux.HandleFunc("/api/loaders/match", corsMiddleware(handleMatch))
	mux.HandleFunc("/api/loaders/", corsMiddleware(handleLoaderDownload))
	mux.HandleFunc("/api/device-logs", corsMiddleware(handleDeviceLog))
	mux.HandleFunc("/api/public/stats", corsMiddleware(handlePublicStats))

	// 扩展公开 API (官网使用)
	mux.HandleFunc("/api/chips", corsMiddleware(handleChips))
	mux.HandleFunc("/api/vendors", corsMiddleware(handleVendors))
	mux.HandleFunc("/api/stats/chips", corsMiddleware(handleStatsChips))
	mux.HandleFunc("/api/stats/vendors", corsMiddleware(handleStatsVendors))
	mux.HandleFunc("/api/stats/hot", corsMiddleware(handleStatsHot))
	mux.HandleFunc("/api/stats/trends", corsMiddleware(handleStatsTrends))
	mux.HandleFunc("/api/stats/overview", corsMiddleware(handleStatsOverview))
	mux.HandleFunc("/api/announcements", corsMiddleware(handleAnnouncements))
	mux.HandleFunc("/api/changelog", corsMiddleware(handleChangelog))
	mux.HandleFunc("/api/feedback", corsMiddleware(handleFeedback))
	mux.HandleFunc("/api/health", corsMiddleware(handleHealth))

	// 高通芯片数据库 API
	mux.HandleFunc("/api/qualcomm/chips", corsMiddleware(handleQualcommChips))
	mux.HandleFunc("/api/qualcomm/stats", corsMiddleware(handleQualcommStats))
	mux.HandleFunc("/api/qualcomm/vendors", corsMiddleware(handleQualcommVendors))

	// MTK 芯片数据库 API
	mux.HandleFunc("/api/mtk/chips", corsMiddleware(handleMtkChips))
	mux.HandleFunc("/api/mtk/stats", corsMiddleware(handleMtkStats))

	// SPD 芯片数据库 API
	mux.HandleFunc("/api/spd/chips", corsMiddleware(handleSpdChips))
	mux.HandleFunc("/api/spd/devices", corsMiddleware(handleSpdDevices))
	mux.HandleFunc("/api/spd/stats", corsMiddleware(handleSpdStats))

	// MTK 设备日志 API (客户端使用 - 类似高通 SAHARA)
	mux.HandleFunc("/api/mtk/device-logs", corsMiddleware(handleMtkDeviceLog))
	mux.HandleFunc("/api/mtk/resources/list", corsMiddleware(handleMtkResourceList))
	mux.HandleFunc("/api/mtk/resources/", corsMiddleware(handleMtkResourceDownload))

	// SPD 设备日志 API (客户端使用)
	mux.HandleFunc("/api/spd/device-logs", corsMiddleware(handleSpdDeviceLog))
	mux.HandleFunc("/api/spd/resources/list", corsMiddleware(handleSpdResourceList))
	mux.HandleFunc("/api/spd/resources/", corsMiddleware(handleSpdResourceDownload))

	// 管理 API (需要认证)
	mux.HandleFunc("/api/admin/loaders", corsMiddleware(authMiddleware(handleAdminLoaders)))
	mux.HandleFunc("/api/admin/loaders/upload", corsMiddleware(authMiddleware(handleUpload)))
	mux.HandleFunc("/api/admin/loaders/", corsMiddleware(authMiddleware(handleAdminLoaderAction)))
	mux.HandleFunc("/api/admin/stats", corsMiddleware(authMiddleware(handleStats)))
	mux.HandleFunc("/api/admin/logs", corsMiddleware(authMiddleware(handleAdminLogs)))
	mux.HandleFunc("/api/admin/login", corsMiddleware(handleLogin))

	// MTK 资源管理 API (需要认证)
	mux.HandleFunc("/api/admin/mtk/resources", corsMiddleware(authMiddleware(handleAdminMtkResources)))
	mux.HandleFunc("/api/admin/mtk/resources/upload", corsMiddleware(authMiddleware(handleMtkResourceUpload)))
	mux.HandleFunc("/api/admin/mtk/resources/", corsMiddleware(authMiddleware(handleAdminMtkResourceAction)))
	mux.HandleFunc("/api/admin/mtk/logs", corsMiddleware(authMiddleware(handleAdminMtkLogs)))
	mux.HandleFunc("/api/admin/mtk/stats", corsMiddleware(authMiddleware(handleAdminMtkStats)))

	// SPD 资源管理 API (需要认证)
	mux.HandleFunc("/api/admin/spd/resources", corsMiddleware(authMiddleware(handleAdminSpdResources)))
	mux.HandleFunc("/api/admin/spd/resources/upload", corsMiddleware(authMiddleware(handleSpdResourceUpload)))
	mux.HandleFunc("/api/admin/spd/resources/", corsMiddleware(authMiddleware(handleAdminSpdResourceAction)))
	mux.HandleFunc("/api/admin/spd/logs", corsMiddleware(authMiddleware(handleAdminSpdLogs)))
	mux.HandleFunc("/api/admin/spd/stats", corsMiddleware(authMiddleware(handleAdminSpdStats)))

	// 静态文件服务 (前端 SPA)
	mux.HandleFunc("/", handleSPA)

	port := ":8082"
	log.Printf("🚀 SakuraEDL Admin API 服务器启动于 http://localhost%s", port)
	log.Printf("📁 上传目录: %s", uploadDir)
	log.Fatal(http.ListenAndServe(port, mux))
}

// ==================== 数据库初始化 ====================

func initDatabase() {
	var err error
	
	// MySQL 连接配置 (从环境变量读取，或使用默认值)
	dbHost := os.Getenv("DB_HOST")
	if dbHost == "" {
		dbHost = "127.0.0.1"
	}
	dbPort := os.Getenv("DB_PORT")
	if dbPort == "" {
		dbPort = "3306"
	}
	dbUser := os.Getenv("DB_USER")
	if dbUser == "" {
		dbUser = "sakuraedl"
	}
	dbPass := os.Getenv("DB_PASS")
	if dbPass == "" {
		dbPass = "071123gan"
	}
	dbName := os.Getenv("DB_NAME")
	if dbName == "" {
		dbName = "sakuraedl"
	}
	
	// MySQL DSN 格式: user:password@tcp(host:port)/database?charset=utf8mb4&parseTime=True
	dsn := fmt.Sprintf("%s:%s@tcp(%s:%s)/%s?charset=utf8mb4&parseTime=True&loc=Local",
		dbUser, dbPass, dbHost, dbPort, dbName)
	
	db, err = sql.Open("mysql", dsn)
	if err != nil {
		log.Fatal("数据库连接失败:", err)
	}
	
	// 测试连接
	if err = db.Ping(); err != nil {
		log.Fatal("数据库连接测试失败:", err)
	}
	
	// 设置连接池
	db.SetMaxOpenConns(25)
	db.SetMaxIdleConns(5)
	db.SetConnMaxLifetime(5 * time.Minute)

	// 创建 loaders 表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS loaders (
			id INT AUTO_INCREMENT PRIMARY KEY,
			filename VARCHAR(255) NOT NULL,
			vendor VARCHAR(100) DEFAULT '',
			chip VARCHAR(100) DEFAULT '',
			hw_id VARCHAR(50) DEFAULT '',
			pk_hash VARCHAR(128) DEFAULT '',
			oem_id VARCHAR(50) DEFAULT '',
			auth_type VARCHAR(20) DEFAULT 'none',
			storage_type VARCHAR(20) DEFAULT 'ufs',
			file_size BIGINT DEFAULT 0,
			file_md5 VARCHAR(64) DEFAULT '',
			file_path VARCHAR(500) DEFAULT '',
			digest_path VARCHAR(500) DEFAULT '',
			sign_path VARCHAR(500) DEFAULT '',
			is_enabled TINYINT DEFAULT 1,
			downloads BIGINT DEFAULT 0,
			match_count BIGINT DEFAULT 0,
			notes TEXT,
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
			INDEX idx_hw_id (hw_id),
			INDEX idx_pk_hash (pk_hash),
			INDEX idx_chip (chip),
			INDEX idx_vendor (vendor)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	`)
	if err != nil {
		log.Fatal("创建 loaders 表失败:", err)
	}

	// 创建 device_logs 表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS device_logs (
			id INT AUTO_INCREMENT PRIMARY KEY,
			platform VARCHAR(50) DEFAULT 'qualcomm',
			sahara_version INT DEFAULT 0,
			msm_id VARCHAR(50) DEFAULT '',
			pk_hash VARCHAR(128) DEFAULT '',
			oem_id VARCHAR(50) DEFAULT '',
			model_id VARCHAR(50) DEFAULT '',
			hw_id VARCHAR(64) DEFAULT '',
			serial_number VARCHAR(50) DEFAULT '',
			chip_name VARCHAR(100) DEFAULT '',
			vendor VARCHAR(100) DEFAULT '',
			storage_type VARCHAR(20) DEFAULT '',
			match_result VARCHAR(50) DEFAULT '',
			loader_id INT,
			client_ip VARCHAR(50) DEFAULT '',
			user_agent VARCHAR(500) DEFAULT '',
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			INDEX idx_msm_id (msm_id),
			INDEX idx_created_at (created_at),
			INDEX idx_match_result (match_result),
			INDEX idx_sahara_version (sahara_version)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	`)
	if err != nil {
		log.Fatal("创建 device_logs 表失败:", err)
	}

	// 添加新列（如果不存在）- 兼容旧表
	db.Exec("ALTER TABLE device_logs ADD COLUMN sahara_version INT DEFAULT 0 AFTER platform")
	db.Exec("ALTER TABLE device_logs ADD COLUMN model_id VARCHAR(50) DEFAULT '' AFTER oem_id")
	db.Exec("ALTER TABLE device_logs ADD COLUMN hw_id VARCHAR(64) DEFAULT '' AFTER model_id")
	db.Exec("ALTER TABLE device_logs ADD COLUMN serial_number VARCHAR(50) DEFAULT '' AFTER hw_id")
	db.Exec("ALTER TABLE device_logs ADD COLUMN chip_name VARCHAR(100) DEFAULT '' AFTER serial_number")
	db.Exec("ALTER TABLE device_logs ADD COLUMN vendor VARCHAR(100) DEFAULT '' AFTER chip_name")

	// 创建 MTK 资源表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS mtk_resources (
			id INT AUTO_INCREMENT PRIMARY KEY,
			resource_type VARCHAR(50) NOT NULL,
			hw_code VARCHAR(50) DEFAULT '',
			chip_name VARCHAR(100) DEFAULT '',
			da_mode VARCHAR(50) DEFAULT '',
			filename VARCHAR(255) NOT NULL,
			file_size BIGINT DEFAULT 0,
			file_md5 VARCHAR(64) DEFAULT '',
			file_path VARCHAR(500) DEFAULT '',
			description TEXT,
			is_enabled TINYINT DEFAULT 1,
			downloads BIGINT DEFAULT 0,
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
			INDEX idx_hw_code (hw_code),
			INDEX idx_chip_name (chip_name),
			INDEX idx_resource_type (resource_type),
			INDEX idx_da_mode (da_mode)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	`)
	if err != nil {
		log.Println("创建 mtk_resources 表失败:", err)
	}

	// 创建 SPD 资源表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS spd_resources (
			id INT AUTO_INCREMENT PRIMARY KEY,
			resource_type VARCHAR(50) NOT NULL,
			chip_id VARCHAR(50) DEFAULT '',
			chip_name VARCHAR(100) DEFAULT '',
			filename VARCHAR(255) NOT NULL,
			file_size BIGINT DEFAULT 0,
			file_md5 VARCHAR(64) DEFAULT '',
			file_path VARCHAR(500) DEFAULT '',
			description TEXT,
			is_enabled TINYINT DEFAULT 1,
			downloads BIGINT DEFAULT 0,
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
			INDEX idx_chip_id (chip_id),
			INDEX idx_chip_name (chip_name),
			INDEX idx_resource_type (resource_type)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	`)
	if err != nil {
		log.Println("创建 spd_resources 表失败:", err)
	}

	// 创建 MTK 设备日志表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS mtk_device_logs (
			id INT AUTO_INCREMENT PRIMARY KEY,
			hw_code VARCHAR(50) DEFAULT '',
			hw_sub_code VARCHAR(50) DEFAULT '',
			hw_version VARCHAR(50) DEFAULT '',
			sw_version VARCHAR(50) DEFAULT '',
			secure_boot VARCHAR(20) DEFAULT '',
			serial_link_auth VARCHAR(20) DEFAULT '',
			daa VARCHAR(20) DEFAULT '',
			chip_name VARCHAR(100) DEFAULT '',
			da_mode VARCHAR(50) DEFAULT '',
			sbc_type VARCHAR(50) DEFAULT '',
			preloader_status VARCHAR(50) DEFAULT '',
			match_result VARCHAR(50) DEFAULT '',
			client_ip VARCHAR(50) DEFAULT '',
			user_agent VARCHAR(500) DEFAULT '',
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			INDEX idx_hw_code (hw_code),
			INDEX idx_chip_name (chip_name),
			INDEX idx_created_at (created_at),
			INDEX idx_match_result (match_result)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	`)
	if err != nil {
		log.Println("创建 mtk_device_logs 表失败:", err)
	}

	// 创建 SPD 设备日志表
	_, err = db.Exec(`
		CREATE TABLE IF NOT EXISTS spd_device_logs (
			id INT AUTO_INCREMENT PRIMARY KEY,
			chip_id VARCHAR(50) DEFAULT '',
			chip_name VARCHAR(100) DEFAULT '',
			fdl1_version VARCHAR(100) DEFAULT '',
			fdl2_version VARCHAR(100) DEFAULT '',
			secure_boot VARCHAR(20) DEFAULT '',
			match_result VARCHAR(50) DEFAULT '',
			client_ip VARCHAR(50) DEFAULT '',
			user_agent VARCHAR(500) DEFAULT '',
			created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
			INDEX idx_chip_id (chip_id),
			INDEX idx_chip_name (chip_name),
			INDEX idx_created_at (created_at),
			INDEX idx_match_result (match_result)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	`)
	if err != nil {
		log.Println("创建 spd_device_logs 表失败:", err)
	}

	log.Println("✅ MySQL 数据库初始化完成")
	log.Printf("📊 数据库连接: %s@%s:%s/%s", dbUser, dbHost, dbPort, dbName)
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
			validToken = "sakuraedl-admin-2024"
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

	// 构建查询 - 使用 is_enabled <> 0 来兼容 MySQL TINYINT
	where := "is_enabled <> 0"
	args := []interface{}{}

	if storageType != "" {
		where += " AND storage_type = ?"
		args = append(args, storageType)
	}
	if vendor != "" {
		where += " AND vendor LIKE ?"
		args = append(args, "%"+vendor+"%")
	}

	query := `SELECT id, filename, vendor, chip, hw_id, auth_type, storage_type, file_size, digest_path, sign_path
		FROM loaders WHERE ` + where + ` ORDER BY vendor, chip, filename`
	
	log.Printf("查询 Loader 列表: %s", query)
	
	rows, err := db.Query(query, args...)
	if err != nil {
		log.Printf("查询 Loader 列表失败: %v", err)
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败: " + err.Error()})
		return
	}
	defer rows.Close()

	loaders := []map[string]interface{}{}
	for rows.Next() {
		var id, fileSize int64
		var filename, vendorVal, chip, hwID, authType, storageTypeVal string
		var digestPath, signPath sql.NullString

		err := rows.Scan(&id, &filename, &vendorVal, &chip, &hwID, &authType, &storageTypeVal, &fileSize, &digestPath, &signPath)
		if err != nil {
			log.Printf("扫描 Loader 行失败: %v", err)
			continue
		}

		// 生成友好显示名称
		displayName := formatLoaderDisplayName(authType, vendorVal, chip)
		
		// 判断是否有 VIP 验证文件
		hasDigest := digestPath.Valid && digestPath.String != ""
		hasSign := signPath.Valid && signPath.String != ""

		loaders = append(loaders, map[string]interface{}{
			"id":           id,
			"filename":     filename,
			"vendor":       vendorVal,
			"chip":         chip,
			"hw_id":        hwID,
			"auth_type":    authType,
			"storage_type": storageTypeVal,
			"file_size":    fileSize,
			"display_name": displayName,
			"has_digest":   hasDigest,
			"has_sign":     hasSign,
		})
	}
	
	log.Printf("查询到 %d 个 Loader", len(loaders))

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
		Platform      string `json:"platform"`
		SaharaVersion int    `json:"sahara_version"` // Sahara 协议版本 (1/2/3)
		MsmID         string `json:"msm_id"`
		PkHash        string `json:"pk_hash"`
		OemID         string `json:"oem_id"`
		ModelID       string `json:"model_id"`
		HwID          string `json:"hw_id"`          // 完整 HWID
		SerialNumber  string `json:"serial_number"`
		ChipName      string `json:"chip_name"`      // 芯片名称 (如 SM8550)
		Vendor        string `json:"vendor"`         // 厂商 (如 Xiaomi, OnePlus)
		StorageType   string `json:"storage_type"`
		MatchResult   string `json:"match_result"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	go logDeviceEx(req.SaharaVersion, req.MsmID, req.PkHash, req.OemID, req.ModelID,
		req.HwID, req.SerialNumber, req.ChipName, req.Vendor, req.StorageType, req.MatchResult, nil, r)

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
		adminPass = "sakuraedl2024"
	}

	if req.Username != adminUser || req.Password != adminPass {
		sendJSON(w, http.StatusUnauthorized, Response{Code: 401, Message: "用户名或密码错误"})
		return
	}

	token := os.Getenv("ADMIN_TOKEN")
	if token == "" {
		token = "sakuraedl-admin-2024"
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
		countQuery := "SELECT COUNT(*) FROM loaders WHERE " + where
		if err := db.QueryRow(countQuery, args...).Scan(&total); err != nil {
			log.Printf("统计 Loader 总数失败: %v", err)
		}
		log.Printf("Loader 总数: %d", total)

		// 获取列表
		queryArgs := append(args, pageSize, (page-1)*pageSize)
		query := `SELECT id, filename, vendor, chip, hw_id, pk_hash, oem_id, auth_type, storage_type,
			       file_size, file_md5, digest_path, sign_path, is_enabled, downloads, match_count,
			       notes, created_at, updated_at
			FROM loaders WHERE ` + where + ` ORDER BY id DESC LIMIT ? OFFSET ?`
		
		log.Printf("管理后台查询: %s, args: %v", query, queryArgs)
		
		rows, err := db.Query(query, queryArgs...)
		if err != nil {
			log.Printf("管理后台查询失败: %v", err)
			sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败: " + err.Error()})
			return
		}
		defer rows.Close()

		loaders := []Loader{}
		for rows.Next() {
			var l Loader
			var digestPath, signPath sql.NullString
			var notes sql.NullString
			var fileMD5 sql.NullString
			var isEnabled int
			var createdAt, updatedAt sql.NullTime

			err := rows.Scan(
				&l.ID, &l.Filename, &l.Vendor, &l.Chip, &l.HwID, &l.PkHash, &l.OemID,
				&l.AuthType, &l.StorageType, &l.FileSize, &fileMD5, &digestPath, &signPath,
				&isEnabled, &l.Downloads, &l.MatchCount, &notes, &createdAt, &updatedAt,
			)
			if err != nil {
				log.Printf("扫描 Loader 数据错误 (ID 可能为空): %v", err)
				continue
			}

			l.IsEnabled = isEnabled != 0
			l.HasDigest = digestPath.Valid && digestPath.String != ""
			l.HasSign = signPath.Valid && signPath.String != ""
			l.Notes = notes.String
			l.FileMD5 = fileMD5.String
			if createdAt.Valid {
				l.CreatedAt = createdAt.Time
			}
			if updatedAt.Valid {
				l.UpdatedAt = updatedAt.Time
			}

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

		var digestPath, signPath sql.NullString
		var filePath sql.NullString
		var notes sql.NullString
		var isEnabled int
		var createdAt, updatedAt time.Time

		err := row.Scan(
			&l.ID, &l.Filename, &l.Vendor, &l.Chip, &l.HwID, &l.PkHash, &l.OemID,
			&l.AuthType, &l.StorageType, &l.FileSize, &l.FileMD5, &filePath,
			&digestPath, &signPath, &isEnabled, &l.Downloads, &l.MatchCount, &notes,
			&createdAt, &updatedAt,
		)
		if err != nil {
			log.Printf("获取 Loader 详情错误: %v", err)
			sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: "Loader 不存在"})
			return
		}

		l.IsEnabled = isEnabled == 1
		l.HasDigest = digestPath.Valid && digestPath.String != ""
		l.HasSign = signPath.Valid && signPath.String != ""
		l.FilePath = filePath.String
		l.Notes = notes.String
		l.CreatedAt = createdAt
		l.UpdatedAt = updatedAt

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
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&logsToday)
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

// 公开统计数据 (无需认证，用于官网展示)
func handlePublicStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	stats := make(map[string]interface{})

	// Loader 统计
	var totalLoaders, enabledLoaders int64
	db.QueryRow("SELECT COUNT(*) FROM loaders").Scan(&totalLoaders)
	db.QueryRow("SELECT COUNT(*) FROM loaders WHERE is_enabled = 1").Scan(&enabledLoaders)
	stats["total_loaders"] = totalLoaders
	stats["enabled_loaders"] = enabledLoaders

	// 设备日志统计
	var totalLogs, logsToday int64
	db.QueryRow("SELECT COUNT(*) FROM device_logs").Scan(&totalLogs)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&logsToday)
	stats["total_logs"] = totalLogs
	stats["logs_today"] = logsToday

	// 按厂商统计
	vendorStats := make(map[string]int64)
	rows, _ := db.Query("SELECT vendor, COUNT(*) FROM loaders WHERE vendor != '' GROUP BY vendor")
	for rows.Next() {
		var vendor string
		var count int64
		rows.Scan(&vendor, &count)
		vendorStats[vendor] = count
	}
	rows.Close()
	stats["vendor_stats"] = vendorStats

	// 最近连接的设备 (仅返回芯片和厂商，隐藏敏感信息)
	recentDevices := []map[string]interface{}{}
	rows, _ = db.Query(`
		SELECT COALESCE(chip_name, ''), COALESCE(vendor, ''), msm_id, storage_type, match_result, created_at 
		FROM device_logs ORDER BY id DESC LIMIT 10
	`)
	for rows.Next() {
		var chipName, vendor, msmID, storageType, matchResult, createdAt string
		rows.Scan(&chipName, &vendor, &msmID, &storageType, &matchResult, &createdAt)
		recentDevices = append(recentDevices, map[string]interface{}{
			"chip_name":    chipName,
			"vendor":       vendor,
			"msm_id":       msmID,
			"storage_type": storageType,
			"match_result": matchResult,
			"created_at":   createdAt,
		})
	}
	rows.Close()
	stats["recent_devices"] = recentDevices

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "获取成功", Data: stats})
}

// ==================== 扩展公开 API (官网使用) ====================

// 获取芯片列表 (从 loaders 表派生)
func handleChips(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	searchQuery := r.URL.Query().Get("q")
	series := r.URL.Query().Get("series")

	// 从 loaders 表查询芯片
	query := `SELECT DISTINCT chip, storage_type, COUNT(*) as loader_count 
		FROM loaders WHERE is_enabled <> 0 AND chip != '' `
	args := []interface{}{}

	if searchQuery != "" {
		query += " AND chip LIKE ? "
		args = append(args, "%"+searchQuery+"%")
	}

	query += " GROUP BY chip, storage_type ORDER BY chip"

	rows, err := db.Query(query, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	chipMap := make(map[string]map[string]interface{})
	for rows.Next() {
		var chip, storageType string
		var loaderCount int
		rows.Scan(&chip, &storageType, &loaderCount)

		chipSeries := extractChipSeries(chip)
		if series != "" && chipSeries != series {
			continue
		}

		if _, ok := chipMap[chip]; !ok {
			chipMap[chip] = map[string]interface{}{
				"name":         chip,
				"series":       chipSeries,
				"storage_type": []string{},
				"loader_count": 0,
				"supported":    true,
			}
		}
		chipMap[chip]["storage_type"] = append(chipMap[chip]["storage_type"].([]string), storageType)
		chipMap[chip]["loader_count"] = chipMap[chip]["loader_count"].(int) + loaderCount
	}

	chips := []map[string]interface{}{}
	for _, chip := range chipMap {
		chips = append(chips, chip)
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"chips": chips, "total": len(chips)},
	})
}

// 获取厂商列表 (从 loaders 表派生)
func handleVendors(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	rows, err := db.Query(`
		SELECT vendor, COUNT(*) as count 
		FROM loaders WHERE is_enabled <> 0 AND vendor != '' 
		GROUP BY vendor ORDER BY count DESC
	`)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	vendors := []map[string]interface{}{}
	for rows.Next() {
		var vendor string
		var count int
		rows.Scan(&vendor, &count)
		vendors = append(vendors, map[string]interface{}{
			"name":    vendor,
			"name_cn": getVendorCN(vendor),
			"count":   count,
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"vendors": vendors, "total": len(vendors)},
	})
}

// 芯片统计
func handleStatsChips(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var total, ufs, emmc int
	db.QueryRow("SELECT COUNT(DISTINCT chip) FROM loaders WHERE is_enabled <> 0 AND chip != ''").Scan(&total)
	db.QueryRow("SELECT COUNT(DISTINCT chip) FROM loaders WHERE is_enabled <> 0 AND chip != '' AND storage_type = 'ufs'").Scan(&ufs)
	db.QueryRow("SELECT COUNT(DISTINCT chip) FROM loaders WHERE is_enabled <> 0 AND chip != '' AND storage_type = 'emmc'").Scan(&emmc)

	// 按系列统计
	rows, _ := db.Query("SELECT chip FROM loaders WHERE is_enabled <> 0 AND chip != '' GROUP BY chip")
	seriesCount := make(map[string]int)
	for rows.Next() {
		var chip string
		rows.Scan(&chip)
		series := extractChipSeries(chip)
		seriesCount[series]++
	}
	rows.Close()

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"total":       total,
			"supported":   total,
			"storage_ufs": ufs,
			"storage_emmc": emmc,
			"by_series":   seriesCount,
		},
	})
}

// 厂商统计
func handleStatsVendors(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	rows, err := db.Query(`
		SELECT vendor, COUNT(*) as count 
		FROM loaders WHERE is_enabled <> 0 AND vendor != '' 
		GROUP BY vendor ORDER BY count DESC
	`)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	vendors := []map[string]interface{}{}
	for rows.Next() {
		var vendor string
		var count int
		rows.Scan(&vendor, &count)
		vendors = append(vendors, map[string]interface{}{
			"name":    vendor,
			"name_cn": getVendorCN(vendor),
			"count":   count,
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"vendors": vendors, "total": len(vendors)},
	})
}

// 热门设备
func handleStatsHot(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	rows, err := db.Query(`
		SELECT msm_id, COALESCE(chip_name, '') as chip_name, COUNT(*) as count 
		FROM device_logs 
		WHERE created_at > DATE_SUB(NOW(), INTERVAL 7 DAY)
		GROUP BY msm_id, chip_name 
		ORDER BY count DESC 
		LIMIT 10
	`)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	devices := []map[string]interface{}{}
	rank := 1
	for rows.Next() {
		var msmID, chipName string
		var count int
		rows.Scan(&msmID, &chipName, &count)
		name := chipName
		if name == "" {
			name = msmID
		}
		devices = append(devices, map[string]interface{}{
			"rank":  rank,
			"chip":  msmID,
			"name":  name,
			"count": count,
		})
		rank++
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"devices": devices, "period": "last_7_days"},
	})
}

// 趋势分析
func handleStatsTrends(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	days := 7
	if d := r.URL.Query().Get("days"); d != "" {
		if parsed, err := strconv.Atoi(d); err == nil && parsed > 0 && parsed <= 30 {
			days = parsed
		}
	}

	rows, err := db.Query(`
		SELECT DATE(created_at) as date, 
			   COUNT(*) as total,
			   SUM(CASE WHEN match_result = 'success' OR match_result = 'matched' THEN 1 ELSE 0 END) as success,
			   SUM(CASE WHEN match_result = 'failed' OR match_result = 'not_found' THEN 1 ELSE 0 END) as failed
		FROM device_logs 
		WHERE created_at > DATE_SUB(NOW(), INTERVAL ? DAY)
		GROUP BY DATE(created_at) 
		ORDER BY date
	`, days)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "查询失败"})
		return
	}
	defer rows.Close()

	trends := []map[string]interface{}{}
	for rows.Next() {
		var date string
		var total, success, failed int
		rows.Scan(&date, &total, &success, &failed)
		trends = append(trends, map[string]interface{}{
			"date":    date,
			"total":   total,
			"success": success,
			"failed":  failed,
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"trends": trends, "period": fmt.Sprintf("last_%d_days", days)},
	})
}

// 总览统计
func handleStatsOverview(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	// 高通统计
	var qcLoaders, qcLogs, qcTodayLogs int
	db.QueryRow("SELECT COUNT(*) FROM loaders WHERE is_enabled <> 0").Scan(&qcLoaders)
	db.QueryRow("SELECT COUNT(*) FROM device_logs").Scan(&qcLogs)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&qcTodayLogs)

	// MTK 统计
	var mtkResources, mtkLogs, mtkTodayLogs int
	db.QueryRow("SELECT COUNT(*) FROM mtk_resources WHERE is_enabled <> 0").Scan(&mtkResources)
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs").Scan(&mtkLogs)
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&mtkTodayLogs)

	// SPD 统计
	var spdResources, spdLogs, spdTodayLogs int
	db.QueryRow("SELECT COUNT(*) FROM spd_resources WHERE is_enabled <> 0").Scan(&spdResources)
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs").Scan(&spdLogs)
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&spdTodayLogs)

	// 最近高通设备
	recentQcDevices := []map[string]interface{}{}
	rows, _ := db.Query(`SELECT msm_id, chip_name, storage_type, match_result, created_at FROM device_logs ORDER BY created_at DESC LIMIT 5`)
	if rows != nil {
		defer rows.Close()
		for rows.Next() {
			var msmID, chipName, storageType, matchResult string
			var createdAt time.Time
			rows.Scan(&msmID, &chipName, &storageType, &matchResult, &createdAt)
			recentQcDevices = append(recentQcDevices, map[string]interface{}{
				"platform":     "qualcomm",
				"chip_id":      msmID,
				"chip_name":    chipName,
				"storage_type": storageType,
				"match_result": matchResult,
				"created_at":   createdAt.Format("2006-01-02 15:04:05"),
			})
		}
	}

	// 最近 MTK 设备
	recentMtkDevices := []map[string]interface{}{}
	rows2, _ := db.Query(`SELECT hw_code, chip_name, da_mode, match_result, created_at FROM mtk_device_logs ORDER BY created_at DESC LIMIT 5`)
	if rows2 != nil {
		defer rows2.Close()
		for rows2.Next() {
			var hwCode, chipName, daMode, matchResult string
			var createdAt time.Time
			rows2.Scan(&hwCode, &chipName, &daMode, &matchResult, &createdAt)
			recentMtkDevices = append(recentMtkDevices, map[string]interface{}{
				"platform":     "mtk",
				"chip_id":      hwCode,
				"chip_name":    chipName,
				"da_mode":      daMode,
				"match_result": matchResult,
				"created_at":   createdAt.Format("2006-01-02 15:04:05"),
			})
		}
	}

	// 最近 SPD 设备
	recentSpdDevices := []map[string]interface{}{}
	rows3, _ := db.Query(`SELECT chip_id, chip_name, secure_boot, match_result, created_at FROM spd_device_logs ORDER BY created_at DESC LIMIT 5`)
	if rows3 != nil {
		defer rows3.Close()
		for rows3.Next() {
			var chipID, chipName, secureBoot, matchResult string
			var createdAt time.Time
			rows3.Scan(&chipID, &chipName, &secureBoot, &matchResult, &createdAt)
			recentSpdDevices = append(recentSpdDevices, map[string]interface{}{
				"platform":     "spd",
				"chip_id":      chipID,
				"chip_name":    chipName,
				"secure_boot":  secureBoot,
				"match_result": matchResult,
				"created_at":   createdAt.Format("2006-01-02 15:04:05"),
			})
		}
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			// 总计
			"total_resources": qcLoaders + mtkResources + spdResources,
			"total_logs":      qcLogs + mtkLogs + spdLogs,
			"today_logs":      qcTodayLogs + mtkTodayLogs + spdTodayLogs,
			// 高通
			"qualcomm": map[string]interface{}{
				"resources":      qcLoaders,
				"logs":           qcLogs,
				"today_logs":     qcTodayLogs,
				"recent_devices": recentQcDevices,
			},
			// MTK
			"mtk": map[string]interface{}{
				"resources":      mtkResources,
				"logs":           mtkLogs,
				"today_logs":     mtkTodayLogs,
				"recent_devices": recentMtkDevices,
			},
			// SPD
			"spd": map[string]interface{}{
				"resources":      spdResources,
				"logs":           spdLogs,
				"today_logs":     spdTodayLogs,
				"recent_devices": recentSpdDevices,
			},
		},
	})
}

// 公告列表
func handleAnnouncements(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	// 静态公告数据 (可以后续改为数据库存储)
	announcements := []map[string]interface{}{
		{"id": 1, "title": "🎉 SakuraEDL v3.0 正式发布", "content": "全新云端 Loader 自动匹配功能上线", "type": "success", "created_at": "2026-01-28"},
		{"id": 2, "title": "📢 新增骁龙8 Elite 支持", "content": "支持最新旗舰芯片 SM8750", "type": "update", "created_at": "2026-01-25"},
		{"id": 3, "title": "💡 OPLUS VIP 认证优化", "content": "改进 VIP 验证流程兼容性", "type": "info", "created_at": "2026-01-20"},
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"announcements": announcements, "total": len(announcements)},
	})
}

// 更新日志
func handleChangelog(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	changelog := []map[string]interface{}{
		{"version": "3.0.0", "date": "2026-01-28", "changes": []string{"云端 Loader 自动匹配", "OPLUS VIP 认证", "全新 UI 界面"}},
		{"version": "2.5.0", "date": "2025-12-01", "changes": []string{"MTK 天玑芯片支持", "内存优化", "Bug 修复"}},
		{"version": "2.0.0", "date": "2025-08-15", "changes": []string{"全新架构重写", "展锐支持", "Fastboot Payload 解析"}},
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"changelog": changelog, "total": len(changelog)},
	})
}

// 用户反馈
func handleFeedback(w http.ResponseWriter, r *http.Request) {
	if r.Method == "GET" {
		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "反馈接口正常"})
		return
	}

	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var req struct {
		Type    string `json:"type"`
		Content string `json:"content"`
		Contact string `json:"contact"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	// 记录反馈 (可以后续存入数据库)
	log.Printf("[Feedback] Type: %s, Content: %s, Contact: %s", req.Type, req.Content, req.Contact)

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "感谢您的反馈！"})
}

// 健康检查
func handleHealth(w http.ResponseWriter, r *http.Request) {
	// 检查数据库连接
	err := db.Ping()
	status := "ok"
	if err != nil {
		status = "error"
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: status,
		Data:    map[string]interface{}{"status": status, "timestamp": time.Now().Unix()},
	})
}

// ==================== 高通芯片数据库 API ====================

// 高通品牌 OEM ID 映射 (基于 qualcomm_database.cs)
var qualcommVendors = map[string]string{
	"0x0000": "Qualcomm",
	"0x0004": "ZTE",
	"0x0011": "Smartisan",
	"0x0015": "Huawei",
	"0x0017": "Lenovo",
	"0x0020": "Samsung",
	"0x0029": "Asus",
	"0x0031": "LG",
	"0x0035": "Nokia",
	"0x0045": "Nokia",
	"0x0051": "OPPO/OnePlus",
	"0x0070": "Google",
	"0x0072": "Xiaomi",
	"0x0073": "Vivo",
	"0x00C8": "Motorola",
	"0x0110": "POCO",
	"0x0200": "Realme",
	"0x0250": "Redmi",
	"0x0260": "Honor",
	"0x0270": "iQOO",
	"0x0290": "Nothing",
	"0x0300": "Sony",
	"0x1043": "Asus",
	"0x50E1": "OnePlus",
	"0x90E1": "OPPO",
	"0xB0E1": "Xiaomi",
}

// 高通芯片数据 (基于 qualcomm_database.cs 真实数据)
var qualcommChips = []map[string]interface{}{
	// Snapdragon 8 Elite
	{"msm_id": "0x0028C0E1", "name": "SM8750", "description": "Snapdragon 8 Elite", "series": "Snapdragon 8", "storage": "UFS 4.0", "process": "3nm", "brands": []string{"Xiaomi", "OnePlus", "Vivo", "OPPO", "Samsung"}},
	{"msm_id": "0x0028D0E1", "name": "SA8750", "description": "Snapdragon 8 Elite", "series": "Snapdragon 8", "storage": "UFS 4.0", "process": "3nm", "brands": []string{"Qualcomm"}},
	// Snapdragon 8 Gen 3
	{"msm_id": "0x0022A0E1", "name": "SM8650", "description": "Snapdragon 8 Gen 3", "series": "Snapdragon 8", "storage": "UFS 4.0", "process": "4nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "Meizu", "Nubia"}},
	{"msm_id": "0x002280E1", "name": "SM8650-AB", "description": "Snapdragon 8 Gen 3", "series": "Snapdragon 8", "storage": "UFS 4.0", "process": "4nm", "brands": []string{"Samsung", "Xiaomi"}},
	// Snapdragon 8s Gen 3
	{"msm_id": "0x0026A0E1", "name": "SM8635", "description": "Snapdragon 8s Gen 3", "series": "Snapdragon 8", "storage": "UFS 4.0", "process": "4nm", "brands": []string{"Xiaomi", "Realme", "iQOO"}},
	// Snapdragon 8 Gen 2
	{"msm_id": "0x001CA0E1", "name": "SM8550", "description": "Snapdragon 8 Gen 2", "series": "Snapdragon 8", "storage": "UFS 4.0", "process": "4nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "Vivo", "OPPO"}},
	// Snapdragon 8+ Gen 1
	{"msm_id": "0x001900E1", "name": "SM8475", "description": "Snapdragon 8+ Gen 1", "series": "Snapdragon 8", "storage": "UFS 3.1", "process": "4nm", "brands": []string{"Xiaomi", "OnePlus", "Asus", "Motorola"}},
	// Snapdragon 8 Gen 1
	{"msm_id": "0x001620E1", "name": "SM8450", "description": "Snapdragon 8 Gen 1", "series": "Snapdragon 8", "storage": "UFS 3.1", "process": "4nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "Motorola", "Sony"}},
	// Snapdragon 888
	{"msm_id": "0x001350E1", "name": "SM8350", "description": "Snapdragon 888", "series": "Snapdragon 8", "storage": "UFS 3.1", "process": "5nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "OPPO", "Vivo", "Asus"}},
	{"msm_id": "0x001360E1", "name": "SM8350-AB", "description": "Snapdragon 888+", "series": "Snapdragon 8", "storage": "UFS 3.1", "process": "5nm", "brands": []string{"Vivo", "Honor", "Asus"}},
	// Snapdragon 865
	{"msm_id": "0x000C30E1", "name": "SM8250", "description": "Snapdragon 865", "series": "Snapdragon 8", "storage": "UFS 3.0", "process": "7nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "OPPO", "Vivo", "Sony", "LG"}},
	{"msm_id": "0x000C40E1", "name": "SM8250-AB", "description": "Snapdragon 865+", "series": "Snapdragon 8", "storage": "UFS 3.0", "process": "7nm", "brands": []string{"Asus", "Lenovo", "Samsung"}},
	// Snapdragon 855
	{"msm_id": "0x000A50E1", "name": "SM8150", "description": "Snapdragon 855", "series": "Snapdragon 8", "storage": "UFS 3.0", "process": "7nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "OPPO", "Vivo", "Sony", "LG"}},
	{"msm_id": "0x000A60E1", "name": "SM8150p", "description": "Snapdragon 855+", "series": "Snapdragon 8", "storage": "UFS 3.0", "process": "7nm", "brands": []string{"OnePlus", "Asus", "Xiaomi"}},
	// Snapdragon 845
	{"msm_id": "0x0008B0E1", "name": "SDM845", "description": "Snapdragon 845", "series": "Snapdragon 8", "storage": "UFS 2.1", "process": "10nm", "brands": []string{"Xiaomi", "OnePlus", "Samsung", "OPPO", "Vivo", "Sony", "LG", "Google"}},
	// Snapdragon 835
	{"msm_id": "0x0005E0E1", "name": "MSM8998", "description": "Snapdragon 835", "series": "Snapdragon 8", "storage": "UFS 2.1", "process": "10nm", "brands": []string{"Samsung", "OnePlus", "Xiaomi", "Sony", "LG", "Google"}},
	// Snapdragon 821/820
	{"msm_id": "0x0005F0E1", "name": "MSM8996Pro", "description": "Snapdragon 821", "series": "Snapdragon 8", "storage": "UFS 2.0", "process": "14nm", "brands": []string{"OnePlus", "Xiaomi", "LG", "Asus", "LeEco"}},
	{"msm_id": "0x009470E1", "name": "MSM8996", "description": "Snapdragon 820", "series": "Snapdragon 8", "storage": "UFS 2.0", "process": "14nm", "brands": []string{"Samsung", "Xiaomi", "LG", "Sony", "HTC"}},
	// Snapdragon 7 系列
	{"msm_id": "0x0025E0E1", "name": "SM7675", "description": "Snapdragon 7+ Gen 3", "series": "Snapdragon 7", "storage": "UFS 4.0", "process": "4nm", "brands": []string{"Realme", "OnePlus", "iQOO"}},
	{"msm_id": "0x0023E0E1", "name": "SM7550", "description": "Snapdragon 7 Gen 3", "series": "Snapdragon 7", "storage": "UFS 3.1", "process": "4nm", "brands": []string{"Xiaomi", "Realme", "Samsung"}},
	{"msm_id": "0x001DF0E1", "name": "SM7450-AB", "description": "Snapdragon 7+ Gen 2", "series": "Snapdragon 7", "storage": "UFS 3.1", "process": "4nm", "brands": []string{"Realme", "OnePlus", "Nothing"}},
	{"msm_id": "0x001DE0E1", "name": "SM7450", "description": "Snapdragon 7 Gen 1", "series": "Snapdragon 7", "storage": "UFS 3.1", "process": "4nm", "brands": []string{"OPPO", "Motorola", "Vivo"}},
	{"msm_id": "0x001CE0E1", "name": "SM7435", "description": "Snapdragon 7s Gen 2", "series": "Snapdragon 7", "storage": "UFS 2.2", "process": "4nm", "brands": []string{"Xiaomi", "Redmi", "POCO"}},
	{"msm_id": "0x001920E1", "name": "SM7325", "description": "Snapdragon 778G", "series": "Snapdragon 7", "storage": "UFS 2.2", "process": "6nm", "brands": []string{"Samsung", "Xiaomi", "Motorola", "OPPO", "Honor"}},
	{"msm_id": "0x001630E1", "name": "SM7350", "description": "Snapdragon 780G", "series": "Snapdragon 7", "storage": "UFS 3.1", "process": "5nm", "brands": []string{"Xiaomi", "Motorola"}},
	{"msm_id": "0x0017C0E1", "name": "SM7225", "description": "Snapdragon 750G", "series": "Snapdragon 7", "storage": "UFS 2.1", "process": "8nm", "brands": []string{"Samsung", "Xiaomi", "OnePlus", "Motorola"}},
	{"msm_id": "0x0011E0E1", "name": "SM7250", "description": "Snapdragon 765G", "series": "Snapdragon 7", "storage": "UFS 2.1", "process": "7nm", "brands": []string{"OnePlus", "Xiaomi", "LG", "OPPO", "Vivo", "Nokia"}},
	{"msm_id": "0x000E70E1", "name": "SM7150", "description": "Snapdragon 730", "series": "Snapdragon 7", "storage": "UFS 2.1", "process": "8nm", "brands": []string{"Xiaomi", "Samsung", "Google", "Realme"}},
	{"msm_id": "0x000DB0E1", "name": "SDM710", "description": "Snapdragon 710", "series": "Snapdragon 7", "storage": "UFS 2.1", "process": "10nm", "brands": []string{"Xiaomi", "OPPO", "Nokia", "Samsung"}},
	// Snapdragon 6 系列
	{"msm_id": "0x002790E1", "name": "SM6550", "description": "Snapdragon 6 Gen 3", "series": "Snapdragon 6", "storage": "UFS 3.1", "process": "4nm", "brands": []string{"Samsung", "Motorola"}},
	{"msm_id": "0x0021E0E1", "name": "SM6450", "description": "Snapdragon 6 Gen 1", "series": "Snapdragon 6", "storage": "UFS 2.2", "process": "4nm", "brands": []string{"OPPO", "Realme", "Motorola"}},
	{"msm_id": "0x0019E0E1", "name": "SM6375", "description": "Snapdragon 695", "series": "Snapdragon 6", "storage": "UFS 2.2", "process": "6nm", "brands": []string{"OPPO", "Realme", "Motorola", "Nokia", "Samsung", "Sony"}},
	{"msm_id": "0x00510000", "name": "SM6375", "description": "Snapdragon 695 (OPPO)", "series": "Snapdragon 6", "storage": "UFS 2.2", "process": "6nm", "brands": []string{"OPPO", "Realme"}},
	{"msm_id": "0x001BE0E1", "name": "SM6225", "description": "Snapdragon 680", "series": "Snapdragon 6", "storage": "eMMC/UFS", "process": "6nm", "brands": []string{"Xiaomi", "Realme", "OPPO", "Samsung", "Motorola"}},
	{"msm_id": "0x0015E0E1", "name": "SM6350", "description": "Snapdragon 690", "series": "Snapdragon 6", "storage": "UFS 2.1", "process": "8nm", "brands": []string{"LG", "Nokia", "TCL"}},
	{"msm_id": "0x000950E1", "name": "SM6150", "description": "Snapdragon 675", "series": "Snapdragon 6", "storage": "UFS 2.1", "process": "11nm", "brands": []string{"Samsung", "Xiaomi", "Realme", "Vivo"}},
	{"msm_id": "0x0010E0E1", "name": "SM6125", "description": "Snapdragon 665", "series": "Snapdragon 6", "storage": "eMMC/UFS", "process": "11nm", "brands": []string{"Xiaomi", "Motorola", "Nokia", "Realme", "OPPO"}},
	{"msm_id": "0x0008C0E1", "name": "SDM660", "description": "Snapdragon 660", "series": "Snapdragon 6", "storage": "eMMC/UFS", "process": "14nm", "brands": []string{"Xiaomi", "Nokia", "OPPO", "Vivo", "Asus"}},
	{"msm_id": "0x000CC0E1", "name": "SDM636", "description": "Snapdragon 636", "series": "Snapdragon 6", "storage": "eMMC/UFS", "process": "14nm", "brands": []string{"Xiaomi", "Nokia", "Asus", "Motorola"}},
	{"msm_id": "0x000460E1", "name": "MSM8953", "description": "Snapdragon 625", "series": "Snapdragon 6", "storage": "eMMC", "process": "14nm", "brands": []string{"Xiaomi", "Motorola", "Samsung", "Nokia", "Asus"}},
	// Snapdragon 4 系列
	{"msm_id": "0x0027A0E1", "name": "SM4550", "description": "Snapdragon 4 Gen 3", "series": "Snapdragon 4", "storage": "UFS 2.2", "process": "4nm", "brands": []string{"Xiaomi", "Redmi"}},
	{"msm_id": "0x001BD0E1", "name": "SM4375", "description": "Snapdragon 4 Gen 2", "series": "Snapdragon 4", "storage": "UFS 2.2", "process": "4nm", "brands": []string{"Xiaomi", "Motorola", "Realme"}},
	{"msm_id": "0x001B90E1", "name": "SM4450", "description": "Snapdragon 4 Gen 1", "series": "Snapdragon 4", "storage": "UFS 2.2", "process": "6nm", "brands": []string{"Motorola", "iQOO", "Samsung"}},
	{"msm_id": "0x001190E1", "name": "SM4350", "description": "Snapdragon 480", "series": "Snapdragon 4", "storage": "UFS 2.1", "process": "8nm", "brands": []string{"Nokia", "Motorola", "OnePlus"}},
	{"msm_id": "0x0013F0E1", "name": "SM4250", "description": "Snapdragon 460", "series": "Snapdragon 4", "storage": "eMMC", "process": "11nm", "brands": []string{"Xiaomi", "Samsung", "Motorola"}},
	{"msm_id": "0x0009A0E1", "name": "SDM450", "description": "Snapdragon 450", "series": "Snapdragon 4", "storage": "eMMC", "process": "14nm", "brands": []string{"Xiaomi", "Asus", "Samsung", "Nokia"}},
	{"msm_id": "0x000BF0E1", "name": "SDM439", "description": "Snapdragon 439", "series": "Snapdragon 4", "storage": "eMMC", "process": "12nm", "brands": []string{"Xiaomi", "Samsung", "Motorola"}},
	{"msm_id": "0x0004F0E1", "name": "MSM8937", "description": "Snapdragon 430", "series": "Snapdragon 4", "storage": "eMMC", "process": "28nm", "brands": []string{"Xiaomi", "Motorola", "Nokia", "Lenovo"}},
	{"msm_id": "0x000510E1", "name": "MSM8917", "description": "Snapdragon 425", "series": "Snapdragon 4", "storage": "eMMC", "process": "28nm", "brands": []string{"Samsung", "Xiaomi", "Motorola", "LG"}},
	// Snapdragon 2xx
	{"msm_id": "0x009600E1", "name": "MSM8909", "description": "Snapdragon 210", "series": "Snapdragon 2", "storage": "eMMC", "process": "28nm", "brands": []string{"Samsung", "Nokia", "Alcatel"}},
	{"msm_id": "0x0015A0E1", "name": "SM4125", "description": "Snapdragon 215", "series": "Snapdragon 2", "storage": "eMMC", "process": "28nm", "brands": []string{"Nokia", "Samsung"}},
	// MDM/SDX 基带
	{"msm_id": "0x002850E1", "name": "SDX80", "description": "X80 5G Modem", "series": "SDX Modem", "storage": "-", "process": "4nm", "brands": []string{"Apple", "Samsung"}},
	{"msm_id": "0x0022D0E1", "name": "SDX75", "description": "X75 5G Modem", "series": "SDX Modem", "storage": "-", "process": "4nm", "brands": []string{"Apple", "Samsung", "OPPO"}},
	{"msm_id": "0x001E30E1", "name": "SDX70", "description": "X70 5G Modem", "series": "SDX Modem", "storage": "-", "process": "4nm", "brands": []string{"Apple", "Samsung"}},
	{"msm_id": "0x001600E1", "name": "SDX65", "description": "X65 5G Modem", "series": "SDX Modem", "storage": "-", "process": "4nm", "brands": []string{"Apple"}},
	{"msm_id": "0x0009E0E1", "name": "SDX55", "description": "X55 5G Modem", "series": "SDX Modem", "storage": "-", "process": "7nm", "brands": []string{"Apple", "Samsung"}},
}

// 高通芯片列表 API
func handleQualcommChips(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	q := r.URL.Query().Get("q")
	series := r.URL.Query().Get("series")
	brand := r.URL.Query().Get("brand")

	result := []map[string]interface{}{}
	for _, chip := range qualcommChips {
		if q != "" {
			name := strings.ToLower(chip["name"].(string))
			desc := strings.ToLower(chip["description"].(string))
			msmId := strings.ToLower(chip["msm_id"].(string))
			if !strings.Contains(name, strings.ToLower(q)) && !strings.Contains(desc, strings.ToLower(q)) && !strings.Contains(msmId, strings.ToLower(q)) {
				continue
			}
		}
		if series != "" && chip["series"] != series {
			continue
		}
		if brand != "" {
			brands := chip["brands"].([]string)
			found := false
			for _, b := range brands {
				if strings.EqualFold(b, brand) {
					found = true
					break
				}
			}
			if !found {
				continue
			}
		}
		result = append(result, chip)
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"chips": result, "total": len(result)},
	})
}

// 高通统计
func handleQualcommStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	total := len(qualcommChips)
	seriesCount := make(map[string]int)
	brandCount := make(map[string]int)

	for _, chip := range qualcommChips {
		if s, ok := chip["series"].(string); ok {
			seriesCount[s]++
		}
		if brands, ok := chip["brands"].([]string); ok {
			for _, brand := range brands {
				brandCount[brand]++
			}
		}
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"total":       total,
			"vendors":     len(qualcommVendors),
			"by_series":   seriesCount,
			"by_brand":    brandCount,
		},
	})
}

// 高通品牌列表
func handleQualcommVendors(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	vendors := []map[string]string{}
	for oemId, name := range qualcommVendors {
		vendors = append(vendors, map[string]string{"oem_id": oemId, "name": name})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"vendors": vendors, "total": len(vendors)},
	})
}

// ==================== MTK 芯片数据库 API ====================

// MTK 芯片数据 (基于 mtk_chip_database.cs 真实数据)
var mtkChips = []map[string]interface{}{
	// Dimensity 9000 系列
	{"hw_code": "0x0950", "name": "MT6989", "description": "Dimensity 9300", "series": "Dimensity 9000", "is_64bit": true, "has_exploit": true, "exploit_type": "AllinoneSignature", "brands": []string{"Vivo", "OPPO", "OnePlus", "Xiaomi"}},
	{"hw_code": "0x1236", "name": "MT6989", "description": "Dimensity 9300 (Preloader)", "series": "Dimensity 9000", "is_64bit": true, "has_exploit": true, "exploit_type": "AllinoneSignature", "brands": []string{"Vivo", "iQOO"}},
	{"hw_code": "0x0930", "name": "MT6985", "description": "Dimensity 9200", "series": "Dimensity 9000", "is_64bit": true, "has_exploit": true, "exploit_type": "AllinoneSignature", "brands": []string{"Vivo", "OPPO", "Xiaomi", "OnePlus"}},
	{"hw_code": "0x0900", "name": "MT6983", "description": "Dimensity 9000", "series": "Dimensity 9000", "is_64bit": true, "has_exploit": true, "exploit_type": "AllinoneSignature", "brands": []string{"OPPO", "Vivo", "Redmi", "Realme"}},
	// Dimensity 8000 系列
	{"hw_code": "0x1172", "name": "MT6895", "description": "Dimensity 8200", "series": "Dimensity 8000", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Redmi", "iQOO", "Realme", "OnePlus"}},
	{"hw_code": "0x0996", "name": "MT6895", "description": "Dimensity 8100", "series": "Dimensity 8000", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "OnePlus", "Realme", "OPPO"}},
	// Dimensity 1000 系列
	{"hw_code": "0x0816", "name": "MT6893", "description": "Dimensity 1200", "series": "Dimensity 1000", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Realme", "OnePlus", "Xiaomi", "Vivo"}},
	{"hw_code": "0x0989", "name": "MT6891", "description": "Dimensity 1100", "series": "Dimensity 1000", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Realme", "OnePlus"}},
	{"hw_code": "0x0886", "name": "MT6885", "description": "Dimensity 1000+", "series": "Dimensity 1000", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "iQOO", "Realme"}},
	// Dimensity 700-900 系列
	{"hw_code": "0x0766", "name": "MT6877", "description": "Dimensity 900", "series": "Dimensity", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Realme", "Vivo"}},
	{"hw_code": "0x0788", "name": "MT6873", "description": "Dimensity 820", "series": "Dimensity", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "Redmi", "Realme"}},
	{"hw_code": "0x0600", "name": "MT6853", "description": "Dimensity 720", "series": "Dimensity", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Realme", "Xiaomi", "Samsung"}},
	{"hw_code": "0x0813", "name": "MT6833", "description": "Dimensity 700", "series": "Dimensity", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Realme", "OPPO", "Redmi", "OnePlus"}},
	// Helio G 系列
	{"hw_code": "0x0588", "name": "MT6785", "description": "Helio G90/G95", "series": "Helio G", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "Redmi", "Realme", "Infinix"}},
	{"hw_code": "0x0551", "name": "MT6768", "description": "Helio G85", "series": "Helio G", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "Realme", "Samsung", "Motorola"}},
	// Helio P 系列
	{"hw_code": "0x0507", "name": "MT6779", "description": "Helio P90", "series": "Helio P", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Realme"}},
	{"hw_code": "0x0688", "name": "MT6771", "description": "Helio P60", "series": "Helio P", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Realme", "Nokia", "Vivo"}},
	{"hw_code": "0x0717", "name": "MT6765", "description": "Helio P35", "series": "Helio P", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "Realme", "Vivo", "OPPO", "Samsung"}},
	{"hw_code": "0x0690", "name": "MT6763", "description": "Helio P23", "series": "Helio P", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"OPPO", "Vivo", "Meizu"}},
	{"hw_code": "0x0707", "name": "MT6762", "description": "Helio P22", "series": "Helio P", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "Realme", "Samsung", "Nokia"}},
	{"hw_code": "0x0601", "name": "MT6757", "description": "Helio P20", "series": "Helio P", "is_64bit": true, "has_exploit": false, "brands": []string{"OPPO", "Vivo", "Meizu"}},
	{"hw_code": "0x0326", "name": "MT6755", "description": "Helio P10", "series": "Helio P", "is_64bit": true, "has_exploit": false, "brands": []string{"Lenovo", "Meizu", "OPPO"}},
	// Helio A 系列
	{"hw_code": "0x0562", "name": "MT6761", "description": "Helio A22", "series": "Helio A", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Xiaomi", "Redmi", "Samsung", "Nokia"}},
	// Helio X 系列
	{"hw_code": "0x0279", "name": "MT6797", "description": "Helio X20/X25", "series": "Helio X", "is_64bit": true, "has_exploit": false, "brands": []string{"Meizu", "LeEco", "Xiaomi"}},
	// 入门级
	{"hw_code": "0x0699", "name": "MT6739", "description": "入门级 4G", "series": "Entry", "is_64bit": true, "has_exploit": true, "exploit_type": "Carbonara", "brands": []string{"Nokia", "Samsung", "Alcatel"}},
	// Legacy
	{"hw_code": "0x0321", "name": "MT6735", "description": "64位四核", "series": "Legacy", "is_64bit": true, "has_exploit": false, "brands": []string{"Xiaomi", "Meizu", "Lenovo"}},
	{"hw_code": "0x0335", "name": "MT6737", "description": "64位四核", "series": "Legacy", "is_64bit": true, "has_exploit": false, "brands": []string{"Samsung", "Lenovo", "ZTE"}},
	{"hw_code": "0x6580", "name": "MT6580", "description": "入门级四核", "series": "Legacy", "is_64bit": false, "has_exploit": false, "brands": []string{"小品牌"}},
	{"hw_code": "0x6572", "name": "MT6572", "description": "双核", "series": "Legacy", "is_64bit": false, "has_exploit": false, "brands": []string{"小品牌"}},
	// MT8xxx 平板系列
	{"hw_code": "0x8173", "name": "MT8173", "description": "Chromebook 芯片", "series": "MT8xxx", "is_64bit": true, "has_exploit": false, "brands": []string{"Lenovo", "Acer", "HP", "Amazon"}},
	{"hw_code": "0x8167", "name": "MT8167", "description": "平板芯片", "series": "MT8xxx", "is_64bit": true, "has_exploit": false, "brands": []string{"Amazon", "Lenovo", "Alcatel"}},
}

// MTK 芯片列表
func handleMtkChips(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	q := r.URL.Query().Get("q")
	series := r.URL.Query().Get("series")
	brand := r.URL.Query().Get("brand")

	result := []map[string]interface{}{}
	for _, chip := range mtkChips {
		if q != "" {
			name := strings.ToLower(chip["name"].(string))
			desc := strings.ToLower(chip["description"].(string))
			hwCode := strings.ToLower(chip["hw_code"].(string))
			if !strings.Contains(name, strings.ToLower(q)) && !strings.Contains(desc, strings.ToLower(q)) && !strings.Contains(hwCode, strings.ToLower(q)) {
				continue
			}
		}
		if series != "" && chip["series"] != series {
			continue
		}
		if brand != "" {
			if brands, ok := chip["brands"].([]string); ok {
				found := false
				for _, b := range brands {
					if strings.EqualFold(b, brand) {
						found = true
						break
					}
				}
				if !found {
					continue
				}
			}
		}
		result = append(result, chip)
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"chips": result, "total": len(result)},
	})
}

// MTK 统计
func handleMtkStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	total := len(mtkChips)
	exploitable := 0
	carbonara := 0
	allinone := 0
	seriesCount := make(map[string]int)
	brandCount := make(map[string]int)

	for _, chip := range mtkChips {
		if hasExploit, ok := chip["has_exploit"].(bool); ok && hasExploit {
			exploitable++
			if exploitType, ok := chip["exploit_type"].(string); ok {
				if exploitType == "Carbonara" {
					carbonara++
				} else if exploitType == "AllinoneSignature" {
					allinone++
				}
			}
		}
		if s, ok := chip["series"].(string); ok {
			seriesCount[s]++
		}
		if brands, ok := chip["brands"].([]string); ok {
			for _, brand := range brands {
				brandCount[brand]++
			}
		}
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"total":       total,
			"exploitable": exploitable,
			"carbonara":   carbonara,
			"allinone":    allinone,
			"by_series":   seriesCount,
			"by_brand":    brandCount,
		},
	})
}

// ==================== SPD 芯片数据库 API ====================

// SPD 芯片数据 (基于 sprd_fdl_database.cs 真实数据)
var spdChips = []map[string]interface{}{
	// SC77xx 系列
	{"chip_id": "0x7731", "name": "SC7731E", "description": "SC7731E (4核 1.3GHz)", "series": "SC77xx", "has_exploit": true, "exploit_id": "0x4ee8", "storage": "eMMC", "brands": []string{"Samsung", "Itel", "ZTE"}},
	{"chip_id": "0x7730", "name": "SC7730", "description": "SC7730 (4核)", "series": "SC77xx", "has_exploit": true, "exploit_id": "0x4ee8", "storage": "eMMC", "brands": []string{"Samsung", "ZTE"}},
	// SC85xx/SC98xx 系列
	{"chip_id": "0x9832", "name": "SC9832E", "description": "SC9832E (4核 A53)", "series": "SC98xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Samsung", "ZTE", "Itel"}},
	{"chip_id": "0x8541", "name": "SC8541E", "description": "SC8541E (4核 A53 LTE)", "series": "SC85xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Samsung", "Blackview", "ZTE"}},
	{"chip_id": "0x9863", "name": "SC9863A", "description": "SC9863A (8核 A55)", "series": "SC98xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC", "brands": []string{"Samsung", "Realme", "Infinix", "Nokia", "Blackview"}},
	{"chip_id": "0x8581", "name": "SC8581A", "description": "SC8581A (8核 A55)", "series": "SC85xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC", "brands": []string{"Samsung", "ZTE"}},
	{"chip_id": "0x9850", "name": "SC9850K", "description": "SC9850K (4核 A53)", "series": "SC98xx", "has_exploit": true, "exploit_id": "0x65015f48", "storage": "eMMC", "brands": []string{"Samsung", "ZTE"}},
	{"chip_id": "0x9860", "name": "SC9860G", "description": "SC9860G (8核 A53)", "series": "SC98xx", "has_exploit": true, "exploit_id": "0x65015f48", "storage": "UFS", "brands": []string{"Samsung"}},
	{"chip_id": "0x9853", "name": "SC9853i", "description": "SC9853i (8核 Intel)", "series": "SC98xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC", "brands": []string{"Leagoo", "Sharp"}},
	// Tiger T6xx 系列
	{"chip_id": "0x0606", "name": "T606", "description": "Tiger T606 (8核 A55)", "series": "T6xx", "has_exploit": false, "storage": "eMMC/UFS", "brands": []string{"Realme", "Motorola", "Nokia"}},
	{"chip_id": "0x0610", "name": "T610", "description": "Tiger T610 (8核 A75+A55)", "series": "T6xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC/UFS", "brands": []string{"Infinix", "Tecno", "Realme"}},
	{"chip_id": "0x0612", "name": "T612", "description": "Tiger T612 (8核 A75+A55)", "series": "T6xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC/UFS", "brands": []string{"Realme", "Infinix"}},
	{"chip_id": "0x0616", "name": "T616", "description": "Tiger T616 (8核 A75+A55)", "series": "T6xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC/UFS", "brands": []string{"Realme", "Infinix", "Motorola"}},
	{"chip_id": "0x0618", "name": "T618", "description": "Tiger T618 (8核 A75+A55)", "series": "T6xx", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC/UFS", "brands": []string{"Realme", "Lenovo", "Teclast"}},
	// Tiger T7xx 系列
	{"chip_id": "0x0700", "name": "T700", "description": "Tiger T700 (8核 A76+A55)", "series": "T7xx", "has_exploit": true, "exploit_id": "0x65012f48", "storage": "eMMC/UFS", "brands": []string{"Realme"}},
	{"chip_id": "0x0740", "name": "T740", "description": "Tanggula T740 (5G)", "series": "T7xx", "has_exploit": false, "storage": "UFS", "brands": []string{"ZTE", "中兴"}},
	{"chip_id": "0x0760", "name": "T760", "description": "Tiger T760 (8核 A76+A55)", "series": "T7xx", "has_exploit": true, "exploit_id": "0x65012f48", "storage": "eMMC/UFS", "brands": []string{"Infinix", "Tecno", "Realme"}},
	{"chip_id": "0x0770", "name": "T770", "description": "Tiger T770 (8核 A76+A55)", "series": "T7xx", "has_exploit": true, "exploit_id": "0x65012f48", "storage": "UFS", "brands": []string{"Realme"}},
	{"chip_id": "0x7520", "name": "T7520", "description": "Tanggula T7520 (5G 旗舰)", "series": "T7xx", "has_exploit": false, "storage": "UFS", "brands": []string{"ZTE", "Honor"}},
	// Tiger T8xx 系列
	{"chip_id": "0x0820", "name": "T820", "description": "Tiger T820 (8核 A78+A55)", "series": "T8xx", "has_exploit": false, "storage": "UFS", "brands": []string{"Realme", "Vivo", "Honor"}},
	{"chip_id": "0x0830", "name": "T830", "description": "Tiger T830 (8核 A78+A55 5G)", "series": "T8xx", "has_exploit": false, "storage": "UFS", "brands": []string{"ZTE"}},
	{"chip_id": "0x0860", "name": "T860", "description": "Tiger T860 (5G 旗舰)", "series": "T8xx", "has_exploit": false, "storage": "UFS", "brands": []string{"Honor", "ZTE"}},
	// Tiger T3xx 系列
	{"chip_id": "0x0310", "name": "T310", "description": "Tiger T310 (4核 A55)", "series": "T3xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Realme", "Nokia", "Itel"}},
	{"chip_id": "0x0320", "name": "T320", "description": "Tiger T320 (4核 A55 增强)", "series": "T3xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Nokia", "Itel"}},
	// Tiger T4xx 系列
	{"chip_id": "0x0403", "name": "T403", "description": "Tiger T403 (6核 A55)", "series": "T4xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Infinix", "Tecno"}},
	{"chip_id": "0x0430", "name": "T430", "description": "Tiger T430 (8核 A55)", "series": "T4xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Infinix", "Tecno", "Itel"}},
	// UMS 系列
	{"chip_id": "0x0312", "name": "UMS312", "description": "UMS312 (T310 变体)", "series": "UMS", "has_exploit": false, "storage": "eMMC", "brands": []string{"Nokia", "Realme"}},
	{"chip_id": "0x0512", "name": "UMS512", "description": "UMS512 (T618 变体)", "series": "UMS", "has_exploit": true, "exploit_id": "0x65015f08", "storage": "eMMC/UFS", "brands": []string{"Realme", "Motorola"}},
	{"chip_id": "0x9230", "name": "UMS9230", "description": "UMS9230 (T606 变体)", "series": "UMS", "has_exploit": false, "storage": "eMMC", "brands": []string{"Realme", "Motorola"}},
	// 功能机系列
	{"chip_id": "0x6531", "name": "SC6531E", "description": "SC6531E (功能机)", "series": "SC65xx", "has_exploit": false, "storage": "NOR Flash", "brands": []string{"Nokia", "Itel", "Samsung"}},
	{"chip_id": "0x6533", "name": "SC6533G", "description": "SC6533G (功能机 4G)", "series": "SC65xx", "has_exploit": false, "storage": "NOR Flash", "brands": []string{"Nokia", "TCL"}},
	{"chip_id": "0x0117", "name": "T117", "description": "T117/UMS9117 (4G 功能机)", "series": "T1xx", "has_exploit": false, "storage": "eMMC", "brands": []string{"Nokia", "Itel", "Lava"}},
}

// SPD 设备数据
var spdDevices = []map[string]interface{}{
	// SC8541E / SC9832E 设备
	{"chip": "SC8541E", "device": "A23-Pro-L5006C", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "A23R", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "A23S-A511LQ", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "A27-A551L", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "A04e", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "A05", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "A24", "brand": "Samsung"},
	{"chip": "SC8541E", "device": "BL50", "brand": "Blackview"},
	{"chip": "SC8541E", "device": "BL51", "brand": "Blackview"},
	// SC9863A 设备
	{"chip": "SC9863A", "device": "BL50-Pro", "brand": "Blackview"},
	{"chip": "SC9863A", "device": "Hot-10i", "brand": "Infinix"},
	{"chip": "SC9863A", "device": "RMX3231", "brand": "Realme"},
	{"chip": "SC9863A", "device": "C21Y", "brand": "Realme"},
	{"chip": "SC9863A", "device": "C25Y", "brand": "Realme"},
	{"chip": "SC9863A", "device": "A03s", "brand": "Samsung"},
	{"chip": "SC9863A", "device": "A04s", "brand": "Samsung"},
	{"chip": "SC9863A", "device": "Nokia-C01-Plus", "brand": "Nokia"},
	{"chip": "SC9863A", "device": "Nokia-C20", "brand": "Nokia"},
	// SC7731E 设备
	{"chip": "SC7731E", "device": "A33-Plus-A509W", "brand": "Samsung"},
	{"chip": "SC7731E", "device": "A02s", "brand": "Samsung"},
	{"chip": "SC7731E", "device": "A03-Core", "brand": "Samsung"},
	// UMS512 设备
	{"chip": "UMS512", "device": "RMX3261", "brand": "Realme"},
	{"chip": "UMS512", "device": "RMX3263", "brand": "Realme"},
	{"chip": "UMS512", "device": "RMX3269", "brand": "Realme"},
	// T610/T612/T616/T618 设备
	{"chip": "T610", "device": "Hot-11-X662", "brand": "Infinix"},
	{"chip": "T610", "device": "Hot-11S", "brand": "Infinix"},
	{"chip": "T610", "device": "Note-11", "brand": "Infinix"},
	{"chip": "T612", "device": "RMX3760", "brand": "Realme"},
	{"chip": "T612", "device": "Note-12-X663", "brand": "Infinix"},
	{"chip": "T616", "device": "RMX3560", "brand": "Realme"},
	{"chip": "T616", "device": "Note-12-Pro", "brand": "Infinix"},
	{"chip": "T618", "device": "Tab-8-X", "brand": "Lenovo"},
	{"chip": "T618", "device": "RMX3085", "brand": "Realme"},
	{"chip": "T618", "device": "Pad-5", "brand": "Realme"},
	// T7xx 设备
	{"chip": "T760", "device": "Note-30-5G", "brand": "Infinix"},
	{"chip": "T770", "device": "11T-Pro", "brand": "Realme"},
	// T8xx 设备
	{"chip": "T820", "device": "GT-5-Pro", "brand": "Realme"},
	{"chip": "T820", "device": "V30", "brand": "Vivo"},
	// UMS9230 / T606 设备
	{"chip": "UMS9230", "device": "RMX3501", "brand": "Realme"},
	{"chip": "UMS9230", "device": "RMX3506", "brand": "Realme"},
	{"chip": "UMS9230", "device": "RMX3511", "brand": "Realme"},
	// 功能机
	{"chip": "SC6531E", "device": "2720-Flip", "brand": "Nokia"},
	{"chip": "SC6531E", "device": "105-4G", "brand": "Nokia"},
	{"chip": "SC6533G", "device": "2760-Flip", "brand": "Nokia"},
	{"chip": "SC6533G", "device": "225-4G", "brand": "Nokia"},
	{"chip": "SC6533G", "device": "6300-4G", "brand": "Nokia"},
}

// SPD 芯片列表
func handleSpdChips(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	q := r.URL.Query().Get("q")
	series := r.URL.Query().Get("series")
	brand := r.URL.Query().Get("brand")

	result := []map[string]interface{}{}
	for _, chip := range spdChips {
		if q != "" {
			name := strings.ToLower(chip["name"].(string))
			desc := strings.ToLower(chip["description"].(string))
			chipId := strings.ToLower(chip["chip_id"].(string))
			if !strings.Contains(name, strings.ToLower(q)) && !strings.Contains(desc, strings.ToLower(q)) && !strings.Contains(chipId, strings.ToLower(q)) {
				continue
			}
		}
		if series != "" && chip["series"] != series {
			continue
		}
		if brand != "" {
			if brands, ok := chip["brands"].([]string); ok {
				found := false
				for _, b := range brands {
					if strings.EqualFold(b, brand) {
						found = true
						break
					}
				}
				if !found {
					continue
				}
			}
		}
		result = append(result, chip)
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"chips": result, "total": len(result)},
	})
}

// SPD 设备列表
func handleSpdDevices(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	q := r.URL.Query().Get("q")
	chip := r.URL.Query().Get("chip")
	brand := r.URL.Query().Get("brand")

	result := []map[string]interface{}{}
	for _, device := range spdDevices {
		if q != "" {
			deviceName := strings.ToLower(device["device"].(string))
			chipName := strings.ToLower(device["chip"].(string))
			brandName := strings.ToLower(device["brand"].(string))
			qLower := strings.ToLower(q)
			if !strings.Contains(deviceName, qLower) && !strings.Contains(chipName, qLower) && !strings.Contains(brandName, qLower) {
				continue
			}
		}
		if chip != "" && device["chip"] != chip {
			continue
		}
		if brand != "" && device["brand"] != brand {
			continue
		}
		result = append(result, device)
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data:    map[string]interface{}{"devices": result, "total": len(result)},
	})
}

// SPD 统计
func handleSpdStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	totalChips := len(spdChips)
	totalDevices := len(spdDevices)
	exploitable := 0
	seriesCount := make(map[string]int)
	brandCount := make(map[string]int)

	for _, chip := range spdChips {
		if hasExploit, ok := chip["has_exploit"].(bool); ok && hasExploit {
			exploitable++
		}
		if s, ok := chip["series"].(string); ok {
			seriesCount[s]++
		}
		if brands, ok := chip["brands"].([]string); ok {
			for _, brand := range brands {
				brandCount[brand]++
			}
		}
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"total_chips":   totalChips,
			"total_devices": totalDevices,
			"exploitable":   exploitable,
			"by_series":     seriesCount,
			"by_brand":      brandCount,
		},
	})
}

// ==================== MTK 设备日志 API ====================

// MTK 设备日志上报 (类似高通 SAHARA)
func handleMtkDeviceLog(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var req struct {
		HwCode          string `json:"hw_code"`
		HwSubCode       string `json:"hw_sub_code"`
		HwVersion       string `json:"hw_version"`
		SwVersion       string `json:"sw_version"`
		SecureBoot      string `json:"secure_boot"`
		SerialLinkAuth  string `json:"serial_link_auth"`
		DAA             string `json:"daa"`
		ChipName        string `json:"chip_name"`
		DaMode          string `json:"da_mode"`
		SbcType         string `json:"sbc_type"`
		PreloaderStatus string `json:"preloader_status"`
		MatchResult     string `json:"match_result"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	go logMtkDevice(req.HwCode, req.HwSubCode, req.HwVersion, req.SwVersion,
		req.SecureBoot, req.SerialLinkAuth, req.DAA, req.ChipName,
		req.DaMode, req.SbcType, req.PreloaderStatus, req.MatchResult, r)

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "日志已记录"})
}

func logMtkDevice(hwCode, hwSubCode, hwVersion, swVersion, secureBoot, serialLinkAuth, daa, chipName, daMode, sbcType, preloaderStatus, matchResult string, r *http.Request) {
	clientIP := r.Header.Get("X-Real-IP")
	if clientIP == "" {
		clientIP = r.Header.Get("X-Forwarded-For")
	}
	if clientIP == "" {
		clientIP = strings.Split(r.RemoteAddr, ":")[0]
	}
	userAgent := r.Header.Get("User-Agent")

	_, err := db.Exec(`
		INSERT INTO mtk_device_logs (hw_code, hw_sub_code, hw_version, sw_version, secure_boot, serial_link_auth, daa, chip_name, da_mode, sbc_type, preloader_status, match_result, client_ip, user_agent)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
	`, hwCode, hwSubCode, hwVersion, swVersion, secureBoot, serialLinkAuth, daa, chipName, daMode, sbcType, preloaderStatus, matchResult, clientIP, userAgent)

	if err != nil {
		log.Printf("MTK 设备日志记录失败: %v", err)
	}
}

// MTK 资源列表 (公开)
func handleMtkResourceList(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	hwCode := r.URL.Query().Get("hw_code")
	resourceType := r.URL.Query().Get("type")
	daMode := r.URL.Query().Get("da_mode")

	where := "is_enabled = 1"
	args := []interface{}{}

	if hwCode != "" {
		where += " AND hw_code = ?"
		args = append(args, hwCode)
	}
	if resourceType != "" {
		where += " AND resource_type = ?"
		args = append(args, resourceType)
	}
	if daMode != "" {
		where += " AND da_mode = ?"
		args = append(args, daMode)
	}

	rows, err := db.Query(`
		SELECT id, resource_type, hw_code, chip_name, da_mode, filename, file_size, file_md5, description
		FROM mtk_resources WHERE `+where+` ORDER BY created_at DESC
	`, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库查询失败"})
		return
	}
	defer rows.Close()

	resources := []map[string]interface{}{}
	for rows.Next() {
		var id int64
		var rType, hwCode, chipName, daMode, filename, fileMd5, description string
		var fileSize int64
		rows.Scan(&id, &rType, &hwCode, &chipName, &daMode, &filename, &fileSize, &fileMd5, &description)
		resources = append(resources, map[string]interface{}{
			"id":            id,
			"resource_type": rType,
			"hw_code":       hwCode,
			"chip_name":     chipName,
			"da_mode":       daMode,
			"filename":      filename,
			"file_size":     fileSize,
			"file_md5":      fileMd5,
			"description":   description,
		})
	}

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "获取成功", Data: map[string]interface{}{"resources": resources}})
}

// MTK 资源下载
func handleMtkResourceDownload(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	idStr := strings.TrimPrefix(r.URL.Path, "/api/mtk/resources/")
	id, err := strconv.ParseInt(idStr, 10, 64)
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的资源 ID"})
		return
	}

	var filePath, filename string
	err = db.QueryRow("SELECT file_path, filename FROM mtk_resources WHERE id = ? AND is_enabled = 1", id).Scan(&filePath, &filename)
	if err != nil {
		sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: "资源不存在"})
		return
	}

	// 更新下载次数
	db.Exec("UPDATE mtk_resources SET downloads = downloads + 1 WHERE id = ?", id)

	w.Header().Set("Content-Disposition", "attachment; filename="+filename)
	http.ServeFile(w, r, filePath)
}

// ==================== SPD 设备日志 API ====================

// SPD 设备日志上报
func handleSpdDeviceLog(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var req struct {
		ChipID      string `json:"chip_id"`
		ChipName    string `json:"chip_name"`
		Fdl1Version string `json:"fdl1_version"`
		Fdl2Version string `json:"fdl2_version"`
		SecureBoot  string `json:"secure_boot"`
		MatchResult string `json:"match_result"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
		return
	}

	go logSpdDevice(req.ChipID, req.ChipName, req.Fdl1Version, req.Fdl2Version, req.SecureBoot, req.MatchResult, r)

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "日志已记录"})
}

func logSpdDevice(chipID, chipName, fdl1Version, fdl2Version, secureBoot, matchResult string, r *http.Request) {
	clientIP := r.Header.Get("X-Real-IP")
	if clientIP == "" {
		clientIP = r.Header.Get("X-Forwarded-For")
	}
	if clientIP == "" {
		clientIP = strings.Split(r.RemoteAddr, ":")[0]
	}
	userAgent := r.Header.Get("User-Agent")

	_, err := db.Exec(`
		INSERT INTO spd_device_logs (chip_id, chip_name, fdl1_version, fdl2_version, secure_boot, match_result, client_ip, user_agent)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)
	`, chipID, chipName, fdl1Version, fdl2Version, secureBoot, matchResult, clientIP, userAgent)

	if err != nil {
		log.Printf("SPD 设备日志记录失败: %v", err)
	}
}

// SPD 资源列表 (公开)
func handleSpdResourceList(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	chipID := r.URL.Query().Get("chip_id")
	resourceType := r.URL.Query().Get("type")

	where := "is_enabled = 1"
	args := []interface{}{}

	if chipID != "" {
		where += " AND chip_id = ?"
		args = append(args, chipID)
	}
	if resourceType != "" {
		where += " AND resource_type = ?"
		args = append(args, resourceType)
	}

	rows, err := db.Query(`
		SELECT id, resource_type, chip_id, chip_name, filename, file_size, file_md5, description
		FROM spd_resources WHERE `+where+` ORDER BY created_at DESC
	`, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库查询失败"})
		return
	}
	defer rows.Close()

	resources := []map[string]interface{}{}
	for rows.Next() {
		var id int64
		var rType, chipID, chipName, filename, fileMd5, description string
		var fileSize int64
		rows.Scan(&id, &rType, &chipID, &chipName, &filename, &fileSize, &fileMd5, &description)
		resources = append(resources, map[string]interface{}{
			"id":            id,
			"resource_type": rType,
			"chip_id":       chipID,
			"chip_name":     chipName,
			"filename":      filename,
			"file_size":     fileSize,
			"file_md5":      fileMd5,
			"description":   description,
		})
	}

	sendJSON(w, http.StatusOK, Response{Code: 0, Message: "获取成功", Data: map[string]interface{}{"resources": resources}})
}

// SPD 资源下载
func handleSpdResourceDownload(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	idStr := strings.TrimPrefix(r.URL.Path, "/api/spd/resources/")
	id, err := strconv.ParseInt(idStr, 10, 64)
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的资源 ID"})
		return
	}

	var filePath, filename string
	err = db.QueryRow("SELECT file_path, filename FROM spd_resources WHERE id = ? AND is_enabled = 1", id).Scan(&filePath, &filename)
	if err != nil {
		sendJSON(w, http.StatusNotFound, Response{Code: 404, Message: "资源不存在"})
		return
	}

	// 更新下载次数
	db.Exec("UPDATE spd_resources SET downloads = downloads + 1 WHERE id = ?", id)

	w.Header().Set("Content-Disposition", "attachment; filename="+filename)
	http.ServeFile(w, r, filePath)
}

// ==================== MTK 管理 API ====================

// MTK 资源管理列表
func handleAdminMtkResources(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	page, _ := strconv.Atoi(r.URL.Query().Get("page"))
	pageSize, _ := strconv.Atoi(r.URL.Query().Get("page_size"))
	keyword := r.URL.Query().Get("keyword")
	resourceType := r.URL.Query().Get("type")

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 200 {
		pageSize = 50
	}

	where := "1=1"
	args := []interface{}{}

	if keyword != "" {
		where += " AND (hw_code LIKE ? OR chip_name LIKE ? OR filename LIKE ?)"
		args = append(args, "%"+keyword+"%", "%"+keyword+"%", "%"+keyword+"%")
	}
	if resourceType != "" {
		where += " AND resource_type = ?"
		args = append(args, resourceType)
	}

	var total int64
	db.QueryRow("SELECT COUNT(*) FROM mtk_resources WHERE "+where, args...).Scan(&total)

	args = append(args, pageSize, (page-1)*pageSize)
	rows, err := db.Query(`
		SELECT id, resource_type, hw_code, chip_name, da_mode, filename, file_size, file_md5, file_path, description, is_enabled, downloads, created_at
		FROM mtk_resources WHERE `+where+` ORDER BY created_at DESC LIMIT ? OFFSET ?
	`, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库查询失败"})
		return
	}
	defer rows.Close()

	resources := []map[string]interface{}{}
	for rows.Next() {
		var id, fileSize, downloads int64
		var rType, hwCode, chipName, daMode, filename, fileMd5, filePath, description string
		var isEnabled int
		var createdAt time.Time
		rows.Scan(&id, &rType, &hwCode, &chipName, &daMode, &filename, &fileSize, &fileMd5, &filePath, &description, &isEnabled, &downloads, &createdAt)
		resources = append(resources, map[string]interface{}{
			"id":            id,
			"resource_type": rType,
			"hw_code":       hwCode,
			"chip_name":     chipName,
			"da_mode":       daMode,
			"filename":      filename,
			"file_size":     fileSize,
			"file_md5":      fileMd5,
			"file_path":     filePath,
			"description":   description,
			"is_enabled":    isEnabled == 1,
			"downloads":     downloads,
			"created_at":    createdAt.Format("2006-01-02 15:04:05"),
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"resources": resources,
			"total":     total,
			"page":      page,
			"page_size": pageSize,
		},
	})
}

// MTK 资源上传
func handleMtkResourceUpload(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	r.ParseMultipartForm(100 << 20) // 100MB

	resourceType := r.FormValue("resource_type")
	hwCode := r.FormValue("hw_code")
	chipName := r.FormValue("chip_name")
	daMode := r.FormValue("da_mode")
	description := r.FormValue("description")

	file, handler, err := r.FormFile("file")
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "文件上传失败"})
		return
	}
	defer file.Close()

	// 计算 MD5
	hash := md5.New()
	fileBytes, _ := io.ReadAll(file)
	hash.Write(fileBytes)
	fileMd5 := hex.EncodeToString(hash.Sum(nil))

	// 保存文件
	savePath := filepath.Join(uploadDir, "mtk", fmt.Sprintf("%s_%s", fileMd5[:8], handler.Filename))
	err = os.WriteFile(savePath, fileBytes, 0644)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "文件保存失败"})
		return
	}

	// 插入数据库
	result, err := db.Exec(`
		INSERT INTO mtk_resources (resource_type, hw_code, chip_name, da_mode, filename, file_size, file_md5, file_path, description)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
	`, resourceType, hwCode, chipName, daMode, handler.Filename, len(fileBytes), fileMd5, savePath, description)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库保存失败"})
		return
	}

	id, _ := result.LastInsertId()
	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "上传成功",
		Data:    map[string]interface{}{"id": id},
	})
}

// MTK 资源操作 (更新/删除)
func handleAdminMtkResourceAction(w http.ResponseWriter, r *http.Request) {
	idStr := strings.TrimPrefix(r.URL.Path, "/api/admin/mtk/resources/")
	id, err := strconv.ParseInt(idStr, 10, 64)
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的资源 ID"})
		return
	}

	switch r.Method {
	case "PUT":
		var req map[string]interface{}
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
			return
		}

		sets := []string{}
		args := []interface{}{}

		if v, ok := req["hw_code"]; ok {
			sets = append(sets, "hw_code = ?")
			args = append(args, v)
		}
		if v, ok := req["chip_name"]; ok {
			sets = append(sets, "chip_name = ?")
			args = append(args, v)
		}
		if v, ok := req["da_mode"]; ok {
			sets = append(sets, "da_mode = ?")
			args = append(args, v)
		}
		if v, ok := req["description"]; ok {
			sets = append(sets, "description = ?")
			args = append(args, v)
		}
		if v, ok := req["is_enabled"]; ok {
			sets = append(sets, "is_enabled = ?")
			if v.(bool) {
				args = append(args, 1)
			} else {
				args = append(args, 0)
			}
		}

		if len(sets) > 0 {
			args = append(args, id)
			_, err = db.Exec("UPDATE mtk_resources SET "+strings.Join(sets, ", ")+" WHERE id = ?", args...)
			if err != nil {
				sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "更新失败"})
				return
			}
		}

		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "更新成功"})

	case "DELETE":
		var filePath string
		db.QueryRow("SELECT file_path FROM mtk_resources WHERE id = ?", id).Scan(&filePath)
		if filePath != "" {
			os.Remove(filePath)
		}
		db.Exec("DELETE FROM mtk_resources WHERE id = ?", id)
		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "删除成功"})

	default:
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
	}
}

// MTK 设备日志列表 (管理)
func handleAdminMtkLogs(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	page, _ := strconv.Atoi(r.URL.Query().Get("page"))
	pageSize, _ := strconv.Atoi(r.URL.Query().Get("page_size"))
	keyword := r.URL.Query().Get("keyword")

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 200 {
		pageSize = 50
	}

	where := "1=1"
	args := []interface{}{}

	if keyword != "" {
		where += " AND (hw_code LIKE ? OR chip_name LIKE ?)"
		args = append(args, "%"+keyword+"%", "%"+keyword+"%")
	}

	var total int64
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs WHERE "+where, args...).Scan(&total)

	// 统计
	var success, notFound, today int64
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs WHERE match_result = 'success'").Scan(&success)
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs WHERE match_result = 'not_found'").Scan(&notFound)
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&today)

	args = append(args, pageSize, (page-1)*pageSize)
	rows, err := db.Query(`
		SELECT id, hw_code, hw_sub_code, hw_version, sw_version, secure_boot, serial_link_auth, daa, chip_name, da_mode, sbc_type, preloader_status, match_result, client_ip, created_at
		FROM mtk_device_logs WHERE `+where+` ORDER BY created_at DESC LIMIT ? OFFSET ?
	`, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库查询失败"})
		return
	}
	defer rows.Close()

	logs := []map[string]interface{}{}
	for rows.Next() {
		var id int64
		var hwCode, hwSubCode, hwVersion, swVersion, secureBoot, serialLinkAuth, daa, chipName, daMode, sbcType, preloaderStatus, matchResult, clientIP string
		var createdAt time.Time
		rows.Scan(&id, &hwCode, &hwSubCode, &hwVersion, &swVersion, &secureBoot, &serialLinkAuth, &daa, &chipName, &daMode, &sbcType, &preloaderStatus, &matchResult, &clientIP, &createdAt)
		logs = append(logs, map[string]interface{}{
			"id":               id,
			"hw_code":          hwCode,
			"hw_sub_code":      hwSubCode,
			"hw_version":       hwVersion,
			"sw_version":       swVersion,
			"secure_boot":      secureBoot,
			"serial_link_auth": serialLinkAuth,
			"daa":              daa,
			"chip_name":        chipName,
			"da_mode":          daMode,
			"sbc_type":         sbcType,
			"preloader_status": preloaderStatus,
			"match_result":     matchResult,
			"client_ip":        clientIP,
			"created_at":       createdAt.Format("2006-01-02 15:04:05"),
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"logs":      logs,
			"total":     total,
			"page":      page,
			"page_size": pageSize,
			"stats": map[string]int64{
				"success":   success,
				"not_found": notFound,
				"today":     today,
			},
		},
	})
}

// MTK 统计 (管理)
func handleAdminMtkStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var totalResources, totalLogs, todayLogs, totalDownloads int64
	db.QueryRow("SELECT COUNT(*) FROM mtk_resources").Scan(&totalResources)
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs").Scan(&totalLogs)
	db.QueryRow("SELECT COUNT(*) FROM mtk_device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&todayLogs)
	db.QueryRow("SELECT COALESCE(SUM(downloads), 0) FROM mtk_resources").Scan(&totalDownloads)

	// 按类型统计
	typeCount := map[string]int64{}
	rows, _ := db.Query("SELECT resource_type, COUNT(*) FROM mtk_resources GROUP BY resource_type")
	if rows != nil {
		defer rows.Close()
		for rows.Next() {
			var rType string
			var count int64
			rows.Scan(&rType, &count)
			typeCount[rType] = count
		}
	}

	// 按芯片统计 Top 10
	chipCount := []map[string]interface{}{}
	rows2, _ := db.Query("SELECT hw_code, chip_name, COUNT(*) as cnt FROM mtk_device_logs GROUP BY hw_code, chip_name ORDER BY cnt DESC LIMIT 10")
	if rows2 != nil {
		defer rows2.Close()
		for rows2.Next() {
			var hwCode, chipName string
			var count int64
			rows2.Scan(&hwCode, &chipName, &count)
			chipCount = append(chipCount, map[string]interface{}{
				"hw_code":   hwCode,
				"chip_name": chipName,
				"count":     count,
			})
		}
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"total_resources": totalResources,
			"total_logs":      totalLogs,
			"today_logs":      todayLogs,
			"total_downloads": totalDownloads,
			"by_type":         typeCount,
			"top_chips":       chipCount,
		},
	})
}

// ==================== SPD 管理 API ====================

// SPD 资源管理列表
func handleAdminSpdResources(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	page, _ := strconv.Atoi(r.URL.Query().Get("page"))
	pageSize, _ := strconv.Atoi(r.URL.Query().Get("page_size"))
	keyword := r.URL.Query().Get("keyword")
	resourceType := r.URL.Query().Get("type")

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 200 {
		pageSize = 50
	}

	where := "1=1"
	args := []interface{}{}

	if keyword != "" {
		where += " AND (chip_id LIKE ? OR chip_name LIKE ? OR filename LIKE ?)"
		args = append(args, "%"+keyword+"%", "%"+keyword+"%", "%"+keyword+"%")
	}
	if resourceType != "" {
		where += " AND resource_type = ?"
		args = append(args, resourceType)
	}

	var total int64
	db.QueryRow("SELECT COUNT(*) FROM spd_resources WHERE "+where, args...).Scan(&total)

	args = append(args, pageSize, (page-1)*pageSize)
	rows, err := db.Query(`
		SELECT id, resource_type, chip_id, chip_name, filename, file_size, file_md5, file_path, description, is_enabled, downloads, created_at
		FROM spd_resources WHERE `+where+` ORDER BY created_at DESC LIMIT ? OFFSET ?
	`, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库查询失败"})
		return
	}
	defer rows.Close()

	resources := []map[string]interface{}{}
	for rows.Next() {
		var id, fileSize, downloads int64
		var rType, chipID, chipName, filename, fileMd5, filePath, description string
		var isEnabled int
		var createdAt time.Time
		rows.Scan(&id, &rType, &chipID, &chipName, &filename, &fileSize, &fileMd5, &filePath, &description, &isEnabled, &downloads, &createdAt)
		resources = append(resources, map[string]interface{}{
			"id":            id,
			"resource_type": rType,
			"chip_id":       chipID,
			"chip_name":     chipName,
			"filename":      filename,
			"file_size":     fileSize,
			"file_md5":      fileMd5,
			"file_path":     filePath,
			"description":   description,
			"is_enabled":    isEnabled == 1,
			"downloads":     downloads,
			"created_at":    createdAt.Format("2006-01-02 15:04:05"),
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"resources": resources,
			"total":     total,
			"page":      page,
			"page_size": pageSize,
		},
	})
}

// SPD 资源上传
func handleSpdResourceUpload(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	r.ParseMultipartForm(100 << 20) // 100MB

	resourceType := r.FormValue("resource_type")
	chipID := r.FormValue("chip_id")
	chipName := r.FormValue("chip_name")
	description := r.FormValue("description")

	file, handler, err := r.FormFile("file")
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "文件上传失败"})
		return
	}
	defer file.Close()

	// 计算 MD5
	hash := md5.New()
	fileBytes, _ := io.ReadAll(file)
	hash.Write(fileBytes)
	fileMd5 := hex.EncodeToString(hash.Sum(nil))

	// 保存文件
	savePath := filepath.Join(uploadDir, "spd", fmt.Sprintf("%s_%s", fileMd5[:8], handler.Filename))
	err = os.WriteFile(savePath, fileBytes, 0644)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "文件保存失败"})
		return
	}

	// 插入数据库
	result, err := db.Exec(`
		INSERT INTO spd_resources (resource_type, chip_id, chip_name, filename, file_size, file_md5, file_path, description)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)
	`, resourceType, chipID, chipName, handler.Filename, len(fileBytes), fileMd5, savePath, description)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库保存失败"})
		return
	}

	id, _ := result.LastInsertId()
	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "上传成功",
		Data:    map[string]interface{}{"id": id},
	})
}

// SPD 资源操作 (更新/删除)
func handleAdminSpdResourceAction(w http.ResponseWriter, r *http.Request) {
	idStr := strings.TrimPrefix(r.URL.Path, "/api/admin/spd/resources/")
	id, err := strconv.ParseInt(idStr, 10, 64)
	if err != nil {
		sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "无效的资源 ID"})
		return
	}

	switch r.Method {
	case "PUT":
		var req map[string]interface{}
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			sendJSON(w, http.StatusBadRequest, Response{Code: 400, Message: "请求格式错误"})
			return
		}

		sets := []string{}
		args := []interface{}{}

		if v, ok := req["chip_id"]; ok {
			sets = append(sets, "chip_id = ?")
			args = append(args, v)
		}
		if v, ok := req["chip_name"]; ok {
			sets = append(sets, "chip_name = ?")
			args = append(args, v)
		}
		if v, ok := req["description"]; ok {
			sets = append(sets, "description = ?")
			args = append(args, v)
		}
		if v, ok := req["is_enabled"]; ok {
			sets = append(sets, "is_enabled = ?")
			if v.(bool) {
				args = append(args, 1)
			} else {
				args = append(args, 0)
			}
		}

		if len(sets) > 0 {
			args = append(args, id)
			_, err = db.Exec("UPDATE spd_resources SET "+strings.Join(sets, ", ")+" WHERE id = ?", args...)
			if err != nil {
				sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "更新失败"})
				return
			}
		}

		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "更新成功"})

	case "DELETE":
		var filePath string
		db.QueryRow("SELECT file_path FROM spd_resources WHERE id = ?", id).Scan(&filePath)
		if filePath != "" {
			os.Remove(filePath)
		}
		db.Exec("DELETE FROM spd_resources WHERE id = ?", id)
		sendJSON(w, http.StatusOK, Response{Code: 0, Message: "删除成功"})

	default:
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
	}
}

// SPD 设备日志列表 (管理)
func handleAdminSpdLogs(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	page, _ := strconv.Atoi(r.URL.Query().Get("page"))
	pageSize, _ := strconv.Atoi(r.URL.Query().Get("page_size"))
	keyword := r.URL.Query().Get("keyword")

	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 200 {
		pageSize = 50
	}

	where := "1=1"
	args := []interface{}{}

	if keyword != "" {
		where += " AND (chip_id LIKE ? OR chip_name LIKE ?)"
		args = append(args, "%"+keyword+"%", "%"+keyword+"%")
	}

	var total int64
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs WHERE "+where, args...).Scan(&total)

	// 统计
	var success, notFound, today int64
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs WHERE match_result = 'success'").Scan(&success)
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs WHERE match_result = 'not_found'").Scan(&notFound)
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&today)

	args = append(args, pageSize, (page-1)*pageSize)
	rows, err := db.Query(`
		SELECT id, chip_id, chip_name, fdl1_version, fdl2_version, secure_boot, match_result, client_ip, created_at
		FROM spd_device_logs WHERE `+where+` ORDER BY created_at DESC LIMIT ? OFFSET ?
	`, args...)
	if err != nil {
		sendJSON(w, http.StatusInternalServerError, Response{Code: 500, Message: "数据库查询失败"})
		return
	}
	defer rows.Close()

	logs := []map[string]interface{}{}
	for rows.Next() {
		var id int64
		var chipID, chipName, fdl1Version, fdl2Version, secureBoot, matchResult, clientIP string
		var createdAt time.Time
		rows.Scan(&id, &chipID, &chipName, &fdl1Version, &fdl2Version, &secureBoot, &matchResult, &clientIP, &createdAt)
		logs = append(logs, map[string]interface{}{
			"id":           id,
			"chip_id":      chipID,
			"chip_name":    chipName,
			"fdl1_version": fdl1Version,
			"fdl2_version": fdl2Version,
			"secure_boot":  secureBoot,
			"match_result": matchResult,
			"client_ip":    clientIP,
			"created_at":   createdAt.Format("2006-01-02 15:04:05"),
		})
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"logs":      logs,
			"total":     total,
			"page":      page,
			"page_size": pageSize,
			"stats": map[string]int64{
				"success":   success,
				"not_found": notFound,
				"today":     today,
			},
		},
	})
}

// SPD 统计 (管理)
func handleAdminSpdStats(w http.ResponseWriter, r *http.Request) {
	if r.Method != "GET" {
		sendJSON(w, http.StatusMethodNotAllowed, Response{Code: 405, Message: "方法不允许"})
		return
	}

	var totalResources, totalLogs, todayLogs, totalDownloads int64
	db.QueryRow("SELECT COUNT(*) FROM spd_resources").Scan(&totalResources)
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs").Scan(&totalLogs)
	db.QueryRow("SELECT COUNT(*) FROM spd_device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&todayLogs)
	db.QueryRow("SELECT COALESCE(SUM(downloads), 0) FROM spd_resources").Scan(&totalDownloads)

	// 按类型统计
	typeCount := map[string]int64{}
	rows, _ := db.Query("SELECT resource_type, COUNT(*) FROM spd_resources GROUP BY resource_type")
	if rows != nil {
		defer rows.Close()
		for rows.Next() {
			var rType string
			var count int64
			rows.Scan(&rType, &count)
			typeCount[rType] = count
		}
	}

	// 按芯片统计 Top 10
	chipCount := []map[string]interface{}{}
	rows2, _ := db.Query("SELECT chip_id, chip_name, COUNT(*) as cnt FROM spd_device_logs GROUP BY chip_id, chip_name ORDER BY cnt DESC LIMIT 10")
	if rows2 != nil {
		defer rows2.Close()
		for rows2.Next() {
			var chipID, chipName string
			var count int64
			rows2.Scan(&chipID, &chipName, &count)
			chipCount = append(chipCount, map[string]interface{}{
				"chip_id":   chipID,
				"chip_name": chipName,
				"count":     count,
			})
		}
	}

	sendJSON(w, http.StatusOK, Response{
		Code:    0,
		Message: "获取成功",
		Data: map[string]interface{}{
			"total_resources": totalResources,
			"total_logs":      totalLogs,
			"today_logs":      todayLogs,
			"total_downloads": totalDownloads,
			"by_type":         typeCount,
			"top_chips":       chipCount,
		},
	})
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
	var success, infoCollected, failed, notFound, today int64
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'success'").Scan(&success)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'info_collected'").Scan(&infoCollected)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'failed'").Scan(&failed)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE match_result = 'not_found'").Scan(&notFound)
	db.QueryRow("SELECT COUNT(*) FROM device_logs WHERE created_at > DATE_SUB(NOW(), INTERVAL 1 DAY)").Scan(&today)
	stats["success"] = success
	stats["info_collected"] = infoCollected
	stats["failed"] = failed
	stats["not_found"] = notFound
	stats["today"] = today

	// 获取日志列表
	queryArgs := append(args, pageSize, (page-1)*pageSize)
	rows, err := db.Query(`
		SELECT id, platform, COALESCE(sahara_version, 0), msm_id, pk_hash, oem_id, 
		       COALESCE(model_id, ''), COALESCE(hw_id, ''), COALESCE(serial_number, ''),
		       COALESCE(chip_name, ''), COALESCE(vendor, ''),
		       storage_type, match_result, loader_id, client_ip, user_agent, created_at
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

		err := rows.Scan(&l.ID, &l.Platform, &l.SaharaVersion, &l.MsmID, &l.PkHash, &l.OemID,
			&l.ModelID, &l.HwID, &l.SerialNumber, &l.ChipName, &l.Vendor,
			&l.StorageType, &l.MatchResult, &loaderID, &l.ClientIP, &l.UserAgent, &createdAt)
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

// 芯片名称映射表
var chipNameMap = map[string]string{
	"SM8750": "骁龙8 Elite",
	"SM8650": "骁龙8 Gen3",
	"SM8550": "骁龙8 Gen2",
	"SM8475": "骁龙8+ Gen1",
	"SM8450": "骁龙8 Gen1",
	"SM8350": "骁龙888",
	"SM8250": "骁龙865",
	"SM8150": "骁龙855",
	"SM7675": "骁龙7+ Gen3",
	"SM7550": "骁龙7 Gen3",
	"SM7475": "骁龙7+ Gen2",
	"SM7450": "骁龙7 Gen1",
	"SM7325": "骁龙778G",
	"SM7250": "骁龙765G",
	"SM7150": "骁龙730",
	"SM6375": "骁龙695",
	"SM6350": "骁龙690",
	"SM6225": "骁龙680",
	"SM6115": "骁龙662",
	"SM4375": "骁龙4 Gen2",
	"SM4350": "骁龙480",
	"SDM845": "骁龙845",
	"SDM835": "骁龙835",
	"SDM670": "骁龙670",
	"SDM660": "骁龙660",
	"MSM8998": "骁龙835",
	"MSM8996": "骁龙820",
	"MSM8953": "骁龙625",
}

// 厂商名称映射表
var vendorNameMap = map[string]string{
	"xiaomi":  "小米",
	"oneplus": "一加",
	"oplus":   "OPLUS",
	"oppo":    "OPPO",
	"realme":  "真我",
	"vivo":    "vivo",
	"samsung": "三星",
	"huawei":  "华为",
	"honor":   "荣耀",
	"meizu":   "魅族",
	"zte":     "中兴",
	"lenovo":  "联想",
	"asus":    "华硕",
	"google":  "Google",
	"motorola": "摩托罗拉",
	"nokia":   "诺基亚",
	"sony":    "索尼",
	"lg":      "LG",
}

// 认证类型映射
var authTypeNameMap = map[string]string{
	"none":    "",
	"miauth":  "小米认证",
	"demacia": "一加认证",
	"vip":     "VIP",
}

// 格式化 Loader 显示名称
func formatLoaderDisplayName(authType, vendor, chip string) string {
	// 获取友好芯片名称
	chipName := chip
	if name, ok := chipNameMap[chip]; ok {
		chipName = name
	}

	// 获取友好厂商名称
	vendorName := strings.ToUpper(vendor)
	if name, ok := vendorNameMap[strings.ToLower(vendor)]; ok {
		vendorName = name
	}

	// 获取认证标签
	authLabel := ""
	if label, ok := authTypeNameMap[authType]; ok && label != "" {
		authLabel = "[" + label + "] "
	}

	return fmt.Sprintf("%s%s %s", authLabel, vendorName, chipName)
}

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
	logDeviceEx(0, msmID, pkHash, oemID, "", "", "", "", "", storageType, matchResult, loaderID, r)
}

func logDeviceEx(saharaVersion int, msmID, pkHash, oemID, modelID, hwID, serialNumber, chipName, vendor, storageType, matchResult string, loaderID *int64, r *http.Request) {
	clientIP := r.RemoteAddr
	if xff := r.Header.Get("X-Forwarded-For"); xff != "" {
		clientIP = strings.Split(xff, ",")[0]
	}
	userAgent := r.UserAgent()

	db.Exec(`
		INSERT INTO device_logs (sahara_version, msm_id, pk_hash, oem_id, model_id, hw_id, serial_number, chip_name, vendor, storage_type, match_result, loader_id, client_ip, user_agent)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
	`, saharaVersion, msmID, pkHash, oemID, modelID, hwID, serialNumber, chipName, vendor, storageType, matchResult, loaderID, clientIP, userAgent)
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

// 从芯片名称提取系列
func extractChipSeries(chipName string) string {
	if chipName == "" {
		return "Other"
	}
	name := strings.ToLower(chipName)

	if strings.Contains(name, "sm8") || strings.Contains(name, "sa8") || strings.Contains(name, "8 gen") || strings.Contains(name, "8elite") {
		return "Snapdragon 8"
	}
	if strings.Contains(name, "sm7") || strings.Contains(name, "7 gen") || strings.Contains(name, "778") || strings.Contains(name, "765") || strings.Contains(name, "710") {
		return "Snapdragon 7"
	}
	if strings.Contains(name, "sm6") || strings.Contains(name, "695") || strings.Contains(name, "680") || strings.Contains(name, "660") || strings.Contains(name, "6 gen") {
		return "Snapdragon 6"
	}
	if strings.Contains(name, "sm4") || strings.Contains(name, "480") || strings.Contains(name, "4 gen") {
		return "Snapdragon 4"
	}
	if strings.Contains(name, "sdm8") || strings.Contains(name, "845") || strings.Contains(name, "835") || strings.Contains(name, "820") {
		return "Snapdragon 8xx"
	}
	if strings.Contains(name, "sdm7") || strings.Contains(name, "730") || strings.Contains(name, "710") {
		return "Snapdragon 7xx"
	}
	if strings.Contains(name, "sdm6") || strings.Contains(name, "625") || strings.Contains(name, "636") || strings.Contains(name, "660") {
		return "Snapdragon 6xx"
	}
	if strings.Contains(name, "sdm4") || strings.Contains(name, "450") || strings.Contains(name, "439") {
		return "Snapdragon 4xx"
	}
	if strings.Contains(name, "apq") || strings.Contains(name, "msm") {
		return "Legacy"
	}

	return "Other"
}

// 获取厂商中文名称
func getVendorCN(vendor string) string {
	v := strings.ToLower(vendor)
	if name, ok := vendorNameMap[v]; ok {
		return name
	}
	return vendor
}

// SPA 静态文件处理 - 支持 Vue Router History 模式
func handleSPA(w http.ResponseWriter, r *http.Request) {
	// 静态文件目录
	staticDir := "./static"
	
	// 获取请求路径
	path := r.URL.Path
	
	// 尝试获取静态文件
	filePath := filepath.Join(staticDir, path)
	
	// 检查文件是否存在
	if info, err := os.Stat(filePath); err == nil && !info.IsDir() {
		// 文件存在，直接返回
		http.ServeFile(w, r, filePath)
		return
	}
	
	// 检查是否是 assets 目录下的文件
	if strings.HasPrefix(path, "/assets/") {
		http.NotFound(w, r)
		return
	}
	
	// 其他所有路径都返回 index.html (SPA fallback)
	indexPath := filepath.Join(staticDir, "index.html")
	if _, err := os.Stat(indexPath); err != nil {
		http.Error(w, "index.html not found", http.StatusNotFound)
		return
	}
	
	http.ServeFile(w, r, indexPath)
}
