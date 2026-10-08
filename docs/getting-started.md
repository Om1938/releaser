# Getting started

## 1. Requirements

- Docker with Docker Compose v2.
- A hostname with TLS in front of Releaser, for production. Any reverse proxy works (Caddy, nginx, Traefik, a cloud load balancer). Releaser itself listens on plain HTTP on port 8080.
- Your existing electron-builder pipeline and artifact hosting (S3, R2, GCS, nginx, a CDN — anything that serves static files over HTTPS).

## 2. Install

```bash
git clone <this repository> releaser && cd releaser
cp .env.example .env        # set POSTGRES_PASSWORD, RELEASER_ADMIN_*, RELEASER_PUBLIC_BASE_URL
docker compose up -d --build --wait
```

On first start Releaser applies its database migrations and creates the bootstrap administrator from `RELEASER_ADMIN_EMAIL` and `RELEASER_ADMIN_PASSWORD`. The bootstrap only happens while no administrator exists. Sign in at `RELEASER_PUBLIC_BASE_URL`, change the password under **Account & system**, then remove the bootstrap password from `.env`.

> Want to see everything working first? Run the demo from the [README](../README.md#quick-start-demo).

## 3. Ship a first targeted release

1. **Create an application.** Go to *Applications → New application*. The **key** (e.g. `acme-desktop`) appears in feed URLs and can't change later. Tick the **supported platforms** the app ships to (e.g. Windows and macOS). Only these can be registered and offered, and you can change them later under *Settings*. The default channel (e.g. `stable`) is what electron-updater's built-in `latest` channel maps to.
2. **Integrate the app once.** Follow [integration-electron.md](integration-electron.md). In short, call `autoUpdater.setFeedURL()` at runtime with `https://<releaser>/u/<appKey>/<installationId>/<currentVersion>/`.
3. **Build and host as usual.** electron-builder writes `latest.yml`, `latest-mac.yml`, `latest-linux.yml` and so on next to your installers. Upload them to your CDN folder for that version.
4. **Register the release.** Go to *Releases → Register release*. Enter the version, tick the platforms you ship now, and give the URL of each selected platform's `latest*.yml`. You can add the other platforms to the same release later from its page (*Add platform manifest*), e.g. Windows today and macOS next week. Releaser fetches each manifest once and validates it. It must carry `sha512` and declare the same version. Releaser then stores an immutable snapshot whose relative file paths are resolved against *your* CDN. Nothing is offered yet.
5. **Define audiences.** For example, *Everyone* (rule "Everyone"), *Customer A* (customer `customer-a`) and *Internal QA* (group `internal-qa`). Customer, user and group rules only match verified identity-context tokens.
6. **Deploy.** Go to *Deployments → New deployment*. Pick the release, channel, audience and percentage, then **Activate**. Within the freshness bound, matching installations are offered the release the next time they check.
7. **Watch, adjust and explain.** Raise the percentage, pause, or use the *Decision explainer* to see what a given installation would receive and why.

## 4. Next steps

- [Operations](operations.md): backups, upgrades and the optional Redis scale-out profile.
- [Precedence and rollouts](precedence-and-rollouts.md): how overlapping deployments, pins and exclusions resolve.
- [Security](security.md): what to put in front of Releaser, and how tokens are trusted.
