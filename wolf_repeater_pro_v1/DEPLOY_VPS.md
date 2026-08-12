# VPS / HTTPS Deployment Guide

## Best free option

Use a free or low-cost VPS such as:
- Oracle Cloud Always Free
- Azure Free Tier (limited)
- Railway or Render (limited free tier, but .NET support varies)

For this project, Oracle Cloud + Ubuntu + Caddy is the most practical and stable setup.

## 1) Create Ubuntu VPS

- Ubuntu 22.04 LTS
- 1 vCPU, 1 GB RAM is enough for this app
- Open ports: 22, 80, 443

## 2) Install .NET 8

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

If the package is not available, use Microsoft packages:

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

## 3) Publish the app

```bash
cd /home/ubuntu/wolf_repeater_pro_v1
sudo dotnet publish -c Release -o /var/www/wolf-repeater
```

## 4) Run as service

Create a service file:

```bash
sudo nano /etc/systemd/system/wolf-repeater.service
```

Content:

```ini
[Unit]
Description=WOLF Repeater Pro
After=network.target

[Service]
WorkingDirectory=/var/www/wolf-repeater
ExecStart=/usr/bin/dotnet /var/www/wolf-repeater/WolfRepeaterPro.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=DOTNET_EnableDiagnostics=0

[Install]
WantedBy=multi-user.target
```

Then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable wolf-repeater
sudo systemctl start wolf-repeater
sudo systemctl status wolf-repeater
```

## 5) Install Caddy for HTTPS

```bash
sudo apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt-get update
sudo apt-get install caddy
```

Create a Caddy config:

```bash
sudo nano /etc/caddy/Caddyfile
```

Use:

```caddyfile
yourdomain.com {
    reverse_proxy localhost:5000
}
```

Then:

```bash
sudo systemctl restart caddy
sudo systemctl status caddy
```

## 6) Open from iPhone

Open the public HTTPS URL in Safari or mobile browser:

```text
https://yourdomain.com
```

This works from cellular data as long as the server is online and DNS points to the VM.

## 7) Important note

This app connects to WOLF accounts and needs a valid login, a live room, and correct WOLF command syntax. The web UI only controls the requests; the real game behavior still depends on the WOLF account and game server.
