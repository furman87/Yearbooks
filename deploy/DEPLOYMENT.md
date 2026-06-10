# Yearbook Viewer Deployment

This guide deploys the Yearbook Viewer to an Ubuntu 24 server using Docker Compose for the app and host nginx for HTTPS reverse proxying.

Production URL:

```text
https://yearbooks.fu87.app
```

## Directory Layout

Keep source code and yearbook data separate:

```text
/opt/furman-yearbooks/          # Git clone / clean code repo
  src/
  tools/
  deploy/
    docker-compose.yml
    .env.production
    nginx.conf

/opt/yearbook-data/
  Bonhomie-1988/
    full/
    thumbnails/
    text/
    preprocess/
```

The Docker image does not contain yearbook images or OCR text. Compose mounts `/opt/yearbook-data` into the container as `/app/yearbooks`.

## Install Packages

Skip the package installation commands that match software already installed on your server.

Check first:

```bash
docker --version
docker compose version
nginx -v
certbot --version
git --version
```

Install shared prerequisites. Skip this step if `ca-certificates`, `curl`, `gnupg`, and `git` are already installed.

```bash
sudo apt update
sudo apt install -y ca-certificates curl gnupg git
```

Install nginx and Certbot. Skip this step if nginx and Certbot are already installed.

```bash
sudo apt install -y nginx certbot python3-certbot-nginx
sudo systemctl enable --now nginx
```

Install Docker and the Docker Compose plugin. Skip this step if `docker --version` and `docker compose version` both work.

```bash
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
```

Optional:

```bash
sudo usermod -aG docker "$USER"
newgrp docker
```

## Copy Or Clone The Code

Create the app directory:

```bash
sudo mkdir -p /opt/furman-yearbooks
sudo chown -R "$USER":"$USER" /opt/furman-yearbooks
```

Preferred Git flow:

```bash
git clone <your-repo-url> /opt/furman-yearbooks
cd /opt/furman-yearbooks/deploy
cp .env.example .env.production
```

If copying from Windows instead of Git, copy only repo files, not the external data folder:

```powershell
scp -r D:\Dev\Yearbooks\src user@your-server:/opt/furman-yearbooks/
scp -r D:\Dev\Yearbooks\tools user@your-server:/opt/furman-yearbooks/
scp -r D:\Dev\Yearbooks\deploy user@your-server:/opt/furman-yearbooks/
scp D:\Dev\Yearbooks\.dockerignore user@your-server:/opt/furman-yearbooks/
scp D:\Dev\Yearbooks\.gitignore user@your-server:/opt/furman-yearbooks/
```

## Add Yearbook Data

Create the external data directory:

```bash
sudo mkdir -p /opt/yearbook-data
sudo chown -R "$USER":"$USER" /opt/yearbook-data
```

Copy yearbook folders into `/opt/yearbook-data`:

```text
/opt/yearbook-data/
  Bonhomie-1988/
    full/
    thumbnails/
    text/
    preprocess/
```

To add a new year later, copy a new `Bonhomie-YYYY` folder into `/opt/yearbook-data`. No rebuild is needed.

## Configure Environment

Edit `/opt/furman-yearbooks/deploy/.env.production`:

```text
YEARBOOK_DATA_PATH=/opt/yearbook-data
AllowedHosts=yearbooks.fu87.app;localhost;127.0.0.1
Viewer__ClickZoomLevel=1.75
```

`YEARBOOK_DATA_PATH` is used by Docker Compose for the host bind mount. `YearbookPath=/app/yearbooks` is the path inside the container.

## Start The App

Run Compose from the `deploy` directory:

```bash
cd /opt/furman-yearbooks/deploy
docker compose --env-file .env.production up -d --build
docker compose ps
curl -i http://127.0.0.1:8387/health
```

Use `curl -i`, not `curl -I`; the health endpoint allows `GET`.

## Configure Nginx

Create `/etc/nginx/sites-available/yearbooks.fu87.app`:

```nginx
server {
    listen 80;
    server_name yearbooks.fu87.app;

    location / {
        proxy_pass http://127.0.0.1:8387;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Enable it:

```bash
sudo ln -s /etc/nginx/sites-available/yearbooks.fu87.app /etc/nginx/sites-enabled/yearbooks.fu87.app
sudo nginx -t
sudo systemctl reload nginx
```

Then request TLS:

```bash
sudo certbot --nginx -d yearbooks.fu87.app
```

## Updates

```bash
cd /opt/furman-yearbooks
git pull
cd deploy
docker compose --env-file .env.production up -d --build
docker compose logs -f furman-yearbooks-viewer
```

Changing only `Viewer__ClickZoomLevel` or another environment value does not require rebuilding:

```bash
cd /opt/furman-yearbooks/deploy
docker compose --env-file .env.production up -d
```

## Useful Commands

```bash
docker compose --env-file .env.production ps
docker compose --env-file .env.production logs -f furman-yearbooks-viewer
docker compose --env-file .env.production restart furman-yearbooks-viewer
curl -i http://127.0.0.1:8387/health
curl -i https://yearbooks.fu87.app/health
```

If no yearbooks appear:

```bash
ls -la /opt/yearbook-data
find /opt/yearbook-data -maxdepth 2 -type d | head
docker compose --env-file .env.production logs furman-yearbooks-viewer
```
