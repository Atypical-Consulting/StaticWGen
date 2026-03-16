# StaticWGen

[![CI](https://github.com/phmatray/StaticWGen/actions/workflows/ci.yml/badge.svg)](https://github.com/phmatray/StaticWGen/actions/workflows/ci.yml)

A static website generator powered by [NUKE](https://nuke.build/), [Markdig](https://github.com/xoofx/markdig), and [Pico CSS](https://picocss.com/).

## Quick Start

```bash
# Build the website
./build.sh BuildWebsite --site-base-url "http://localhost:8080" --site-title "My Site"

# Build and deploy with Docker
./build.sh DeployDockerImage \
  --site-base-url "http://localhost:8080" \
  --site-title "My Site" \
  --image-name my-site \
  --version-tag latest \
  --container-name my-site \
  --host-port 8080 \
  --container-port 80
```

## Features

- Markdown to HTML conversion with YAML front-matter metadata
- Emoji, mathematics (LaTeX), Prism.js syntax highlighting, and Mermaid diagrams
- Automatic sitemap.xml, robots.txt, and Atom feed generation
- Tagging system with tag index and individual tag pages
- Docker deployment with nginx
- GitHub Actions CI/CD with GitHub Pages deployment
