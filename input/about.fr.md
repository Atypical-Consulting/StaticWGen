---
title: "A propos de StaticWGen"
description: "Decouvrez l'architecture, les fonctionnalites et la philosophie de StaticWGen."
author: "Philippe Matray"
date: "2024-09-13"
keywords: "about, architecture, features, nuke, markdig, pico css, static site generator"
image: "./assets/logo-wgen.webp"
lang: fr
translationOf: about
---
# A propos de StaticWGen

StaticWGen est un generateur de sites statiques construit entierement sur l'ecosysteme .NET. Il a ete cree pour prouver qu'il n'est pas necessaire d'utiliser une toolchain JavaScript pour construire des sites statiques modernes et riches en fonctionnalites.

## Architecture

Le pipeline de build est orchestre par NUKE et suit une conception composable :

```mermaid
flowchart TB
    subgraph Entree
        MD[Markdown + YAML Front Matter]
        TPL[Template Scriban]
        ASSETS[CSS / JS / Images]
    end
    subgraph Pipeline
        PARSE[Analyse et Extraction Metadata]
        FILTER[Filtre: Brouillon / Planifie / Archive]
        RENDER[Rendu HTML via Scriban]
        TAGS[Generation Pages de Tags]
        BLOG[Generation Index Blog]
        SEARCH[Construction Index Recherche]
        SEO[Sitemap + Robots + Flux]
    end
    subgraph Sortie
        HTML[Fichiers HTML Statiques]
        JSON[search-index.json]
        XML[sitemap.xml + feed.xml]
    end
    MD --> PARSE --> FILTER --> RENDER --> HTML
    TPL --> RENDER
    ASSETS --> HTML
    RENDER --> TAGS --> HTML
    RENDER --> BLOG --> HTML
    RENDER --> SEARCH --> JSON
    RENDER --> SEO --> XML
```

## Extensions Markdown

StaticWGen utilise Markdig avec un ensemble d'extensions soigneusement choisies :

- **YAML Front Matter** --- titre, description, auteur, date, tags et metadonnees personnalisees
- **Coloration Syntaxique** --- 200+ langages via Prism.js
- **Diagrammes Mermaid** --- organigrammes, diagrammes de sequence, machines d'etat
- **Mathematiques LaTeX** --- en ligne \( E = mc^2 \) et en mode affichage
- **Emoji** --- ecrivez `:rocket:` et obtenez :rocket:
- **SmartyPants** --- guillemets et tirets typographiques
- **Tableaux, Notes de Bas de Page, Listes de Taches**

## Cycle de Vie du Contenu

| Statut | Comportement |
|--------|-------------|
| **Publie** | Visible sur le site, inclus dans les flux et la recherche |
| **Brouillon** | Cache sauf si construit avec `--include-drafts` |
| **Planifie** | Auto-publication quand la `publishDate` arrive |
| **Archive** | Accessible par URL mais exclu de la navigation |

## Philosophie

1. **Le contenu d'abord** --- le Markdown est la source de verite
2. **Pas de magie** --- chaque etape est du code C# explicite
3. **Dependances minimales** --- Pico CSS + JS vanilla, pas de framework
4. **Deployer partout** --- Docker, GitHub Pages, S3, ou `cp -r output/ /var/www`
