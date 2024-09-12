# Static Website Generator

## Description

This is a simple static website generator powered by [NUKE](https://nuke.build).
The generator takes a markdown file as input and generates a static HTML file as output.

<img src="./assets/logo-wgen.webp" alt="Static Website Generator" style="max-width: 400px;">

## Why?

I wanted to learn how to build a static website generator and I wanted to learn how to use NUKE. Combining the two
seemed like a good idea.

## How?

The generator is built using NUKE and the NUKE build script is written in C#. The generator takes a markdown file as
input and generates a static HTML file as output. The generator uses the Markdig library to parse the markdown file and
generate the HTML file.

## Usage

To generate a static HTML file from a markdown file, run the following command:

```
nuke GenerateWebsite
```