# Deploy on Railway

This project is configured to run on Railway with Docker.

## 1) Push code to GitHub

Railway deploys from your repository.

## 2) Create project in Railway

- New Project -> Deploy from GitHub repo
- Select this repository

## 3) Set environment variables in Railway

In Railway service Variables, add:

- `DATABASE_URL` = your Render PostgreSQL connection string
- `ASPNETCORE_ENVIRONMENT` = `Production`

If your Render database requires SSL, ensure the URL includes `?sslmode=require`.

## 4) Deploy

Railway will read `railway.toml` and use the Dockerfile in this repo.

## 5) Verify

- Open Railway deployment logs
- Confirm app started and is listening on port from `PORT`
- Open generated Railway domain

