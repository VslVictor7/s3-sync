package main

import (
	"bufio"
	"fmt"
	"log"
	"os"
	"os/exec"
	"strings"
	"time"
)

var (
	LogDir    = "/var/log"
	timestamp = time.Now().Format("2006-01-02-15-04")
	logFile   = fmt.Sprintf("%s/rclone_sync_%s.log", LogDir, timestamp)

	bucketsSelecionados = map[string]bool{
		"ente-photos": true,
		"ente-auth":   true,
		"vaultwarden": true,
		"jellystat":   true,
		"jellyfin":    true,
	}
)

func logMsg(msg string) {
	fmt.Println(msg)

	f, err := os.OpenFile(logFile, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		log.Fatal(err)
	}
	defer f.Close()

	f.WriteString(msg + "\n")
}

func listarBuckets() ([]string, error) {
	cmd := exec.Command("rclone", "lsd", "minio:")
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		return nil, err
	}

	if err := cmd.Start(); err != nil {
		return nil, err
	}

	var buckets []string
	scanner := bufio.NewScanner(stdout)

	for scanner.Scan() {
		parts := strings.Fields(scanner.Text())
		if len(parts) > 0 {
			bucket := parts[len(parts)-1]
			buckets = append(buckets, bucket)
		}
	}

	if err := cmd.Wait(); err != nil {
		return nil, err
	}

	return buckets, nil
}

func sincronizarBucket(bucket string) {
	logMsg(fmt.Sprintf("Sincronizando %s...", bucket))

	cmd := exec.Command(
		"rclone", "sync",
		fmt.Sprintf("minio:%s", bucket),
		fmt.Sprintf("gdrive:backups/%s", bucket),
		"--transfers=8",
		"--checkers=8",
		"--drive-use-trash=false",
		"--max-delete", "1000",
		"--log-file", logFile,
		"--log-level", "INFO",
	)

	err := cmd.Run()
	if err != nil {
		logMsg(fmt.Sprintf("[ERRO] Erro ao sincronizar %s: %v\n", bucket, err))
		return
	}

	logMsg(fmt.Sprintf("[OK] %s sincronizado com sucesso.\n", bucket))
}

func main() {
	os.MkdirAll(LogDir, 0755)
	logMsg(fmt.Sprintf("=== %s ===", time.Now().String()))

	buckets, err := listarBuckets()
	if err != nil {
		logMsg(fmt.Sprintf("Erro ao listar buckets: %v", err))
		return
	}

	var selecionados []string
	for _, b := range buckets {
		if bucketsSelecionados[b] {
			selecionados = append(selecionados, b)
		}
	}

	if len(selecionados) == 0 {
		logMsg("Nenhum bucket selecionado encontrado no MinIO.")
		return
	}

	for _, bucket := range selecionados {
		sincronizarBucket(bucket)
	}

	logMsg("Sincronização concluída.")
}
