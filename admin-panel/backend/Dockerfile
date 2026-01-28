# MultiFlash Admin Backend Dockerfile
FROM golang:1.21-alpine AS builder

WORKDIR /app

# 安装依赖
RUN apk add --no-cache git

# 复制 go.mod 和 go.sum
COPY go.mod go.sum ./
RUN go mod download

# 复制源代码
COPY . .

# 编译
RUN CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o multiflash-admin .

# 运行阶段
FROM alpine:latest

RUN apk --no-cache add ca-certificates tzdata

WORKDIR /app

# 复制编译好的二进制
COPY --from=builder /app/multiflash-admin .
COPY --from=builder /app/static ./static

# 创建上传目录
RUN mkdir -p uploads/loaders uploads/digest uploads/sign

# 设置时区
ENV TZ=Asia/Shanghai

# 暴露端口
EXPOSE 8082

# 运行
CMD ["./multiflash-admin"]
