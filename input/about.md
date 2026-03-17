---
title: "About This Demo"
description: "Learn more about this demo website and the tools used to create it."
author: "Philippe Matray"
date: "2024-09-13"
keywords: "about, demo, nuke, pico css"
image: "./assets/logo-wgen.webp"
lang: en
translationOf: about
---
# About This Demo

Welcome to the demo website! This site is generated using **Nuke**, a powerful build automation tool, and **Pico CSS**
for styling.

## What is Nuke?

Nuke is a cross-platform build automation system that helps manage complex build pipelines. With Nuke, you can automate
tasks like:

- Building your projects
- Running tests
- Generating static websites (like this one)
- Packaging and deploying applications

![youtube.com](https://www.youtube.com/watch?v=E7CufNR84M4)

## Why Pico CSS?

[Pico CSS](https://picocss.com) is a lightweight, minimalist CSS framework. It offers a great balance between simplicity
and elegance, making it easy to style static websites without writing extensive CSS code.

## Parsing Markdown Files

This demo uses the `Markdig` library to parse Markdown files. This allows you to write content in Markdown format
and have some nice extensions like `Mermaid` diagrams or `Emoji` support. ;)

```mermaid
graph TD;
    A-->B;
    A-->C;
    B-->D;
    C-->D;
```

## Support for Prism.js

This demo also includes support for syntax highlighting using `Prism.js`. You can easily add code blocks to your Markdown
files and have them automatically highlighted.

```csharp
public class HelloWorld
{
    public static void Main()
    {
        Console.WriteLine("Hello, World!");
    }
}
```

## Features of This Demo

- Static site generation from Markdown files
- Automated build process with Nuke
- Simple styling using Pico CSS
- Docker containerization for local deployment
