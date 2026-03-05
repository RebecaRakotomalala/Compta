# Deploy on Render

This project is ready to deploy on Render with Docker.

## 1) Push code to GitHub

Render deploys from your repository.

## 2) Create Web Service

- In Render: New -> Web Service
- Connect your GitHub repo
- Render detects `render.yaml` automatically

## 3) Set environment variables

In your Render Web Service, set:

- `DATABASE_URL` = PostgreSQL internal or external URL from your Render database
- `ASPNETCORE_ENVIRONMENT` = `Production`

If needed, ensure SSL is enabled in the URL (for example `?sslmode=require`).

## 4) Deploy and verify

- Trigger deploy
- Check logs for startup success
- Open the generated Render URL

## 5) Existing Render PostgreSQL

If your PostgreSQL already exists on Render, reuse its connection string in
`DATABASE_URL` (no code change required).

