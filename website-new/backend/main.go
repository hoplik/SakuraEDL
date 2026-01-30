// SakuraEDL Website - 官网后端 (静态文件服务 + API 代理)
package main

import (
	"io"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

var (
	apiBaseURL = "https://api.sakuraedl.org"
	staticDir  = "./static"
	port       = ":8080"
)

func main() {
	log.SetFlags(log.LstdFlags | log.Lshortfile)

	// 从环境变量读取配置
	if p := os.Getenv("PORT"); p != "" {
		port = ":" + p
	}
	if dir := os.Getenv("STATIC_DIR"); dir != "" {
		staticDir = dir
	}
	if api := os.Getenv("API_BASE_URL"); api != "" {
		apiBaseURL = api
	}

	mux := http.NewServeMux()

	// API 代理 - 转发所有 /api 请求到 api.sakuraedl.org
	mux.HandleFunc("/api/", handleAPIProxy)

	// 下载文件服务 - 驱动和工具下载
	mux.HandleFunc("/downloads/", handleDownloads)
	mux.HandleFunc("/qualcomm/", handleDownloads)
	mux.HandleFunc("/mediatek/", handleDownloads)
	mux.HandleFunc("/spreadtrum/", handleDownloads)

	// 静态文件服务 (SPA 模式)
	mux.HandleFunc("/", handleSPA)

	log.Printf("🌸 SakuraEDL Website 启动于 http://localhost%s", port)
	log.Printf("📁 静态目录: %s", staticDir)
	log.Printf("🔗 API 代理: %s", apiBaseURL)

	server := &http.Server{
		Addr:         port,
		Handler:      mux,
		ReadTimeout:  30 * time.Second,
		WriteTimeout: 30 * time.Second,
	}

	log.Fatal(server.ListenAndServe())
}

// API 代理处理器
func handleAPIProxy(w http.ResponseWriter, r *http.Request) {
	// CORS 处理
	w.Header().Set("Access-Control-Allow-Origin", "*")
	w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
	w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Admin-Token")

	if r.Method == "OPTIONS" {
		w.WriteHeader(http.StatusOK)
		return
	}

	// 构建目标 URL
	targetURL := apiBaseURL + r.URL.Path
	if r.URL.RawQuery != "" {
		targetURL += "?" + r.URL.RawQuery
	}

	// 创建代理请求
	proxyReq, err := http.NewRequest(r.Method, targetURL, r.Body)
	if err != nil {
		http.Error(w, "代理请求创建失败", http.StatusInternalServerError)
		return
	}

	// 复制请求头
	for key, values := range r.Header {
		for _, value := range values {
			proxyReq.Header.Add(key, value)
		}
	}
	proxyReq.Header.Set("X-Forwarded-For", r.RemoteAddr)

	// 发送请求
	client := &http.Client{Timeout: 30 * time.Second}
	resp, err := client.Do(proxyReq)
	if err != nil {
		log.Printf("[Proxy] 请求失败: %s -> %v", targetURL, err)
		http.Error(w, "API 请求失败", http.StatusBadGateway)
		return
	}
	defer resp.Body.Close()

	// 复制响应头
	for key, values := range resp.Header {
		for _, value := range values {
			w.Header().Add(key, value)
		}
	}
	w.WriteHeader(resp.StatusCode)

	// 复制响应体
	io.Copy(w, resp.Body)
}

// 下载文件处理
func handleDownloads(w http.ResponseWriter, r *http.Request) {
	// 下载目录映射
	downloadDir := "./downloads"
	if dir := os.Getenv("DOWNLOAD_DIR"); dir != "" {
		downloadDir = dir
	}

	// 获取请求路径，去掉前缀
	path := r.URL.Path
	// 去掉 /downloads/, /qualcomm/, /mediatek/, /spreadtrum/ 前缀
	path = strings.TrimPrefix(path, "/downloads")
	path = strings.TrimPrefix(path, "/")

	// 构建文件路径
	filePath := filepath.Join(downloadDir, path)

	// 安全检查：防止目录遍历
	absDownloadDir, _ := filepath.Abs(downloadDir)
	absFilePath, _ := filepath.Abs(filePath)
	if !strings.HasPrefix(absFilePath, absDownloadDir) {
		http.Error(w, "403 Forbidden", http.StatusForbidden)
		return
	}

	// 检查文件是否存在
	info, err := os.Stat(filePath)
	if err != nil || info.IsDir() {
		log.Printf("[Download] 文件不存在: %s", filePath)
		http.Error(w, "404 Not Found - 文件不存在，请联系管理员上传", http.StatusNotFound)
		return
	}

	// 设置下载响应头
	filename := filepath.Base(filePath)
	w.Header().Set("Content-Disposition", "attachment; filename=\""+filename+"\"")
	w.Header().Set("Content-Type", "application/octet-stream")

	log.Printf("[Download] 下载文件: %s (%d bytes)", filename, info.Size())

	http.ServeFile(w, r, filePath)
}

// SPA 静态文件处理 (支持 Vue Router History 模式)
func handleSPA(w http.ResponseWriter, r *http.Request) {
	path := r.URL.Path

	// 尝试获取静态文件
	filePath := filepath.Join(staticDir, path)

	// 检查文件是否存在
	if info, err := os.Stat(filePath); err == nil && !info.IsDir() {
		http.ServeFile(w, r, filePath)
		return
	}

	// assets 目录下的文件不存在则返回 404
	if strings.HasPrefix(path, "/assets/") {
		http.NotFound(w, r)
		return
	}

	// 其他路径返回 index.html (SPA fallback)
	indexPath := filepath.Join(staticDir, "index.html")
	if _, err := os.Stat(indexPath); err != nil {
		http.Error(w, "index.html not found", http.StatusNotFound)
		return
	}

	http.ServeFile(w, r, indexPath)
}
