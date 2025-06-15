# Log Analysis Tool - Overview

## Project Overview

This plan outlines a comprehensive .NET command-line application for analyzing log files from microservice-based systems using AI. It is designed for developers, DevOps engineers, and Site Reliability Engineers (SREs) to streamline troubleshooting, enhance system observability, and enable proactive issue detection.

## Key Features

The tool features project-based organization, natural language querying, intelligent anomaly detection, and rich DocFX-compatible reporting. The application ingests multiple log files via glob patterns, parses heterogeneous log formats, normalizes them to a common schema, and applies AI-driven analysis through specialized pattern-based prompts.

## Primary Goals

- **Reduce MTTR**: Reduce mean time to resolution (MTTR) for incidents
- **Deep Insights**: Provide deeper insights into complex system behaviors  
- **Proactive Detection**: Identify potential problems before they impact users
- **Unified Analysis**: Streamline analysis across heterogeneous log sources

## Target Users

- **Developers**: Troubleshooting application issues and understanding system behavior
- **DevOps Engineers**: Monitoring deployment health and infrastructure performance
- **Site Reliability Engineers (SREs)**: Maintaining system reliability and investigating incidents

## Architecture Philosophy

The application uses a unified project-based approach where each analysis run is tracked within the project context. This eliminates the complexity of separate session management while maintaining full historical tracking and comparison capabilities.
