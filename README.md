# BYOH Host Service

This repository is a sanitized code sample from a platform built in 2019-2023. It is included to demonstrate architecture, systems integration, cloud orchestration, and ownership of a complex production platform. It is not intended to represent my current preferred stack or coding conventions.

Build Your Own Home (BYOH) was an online, interactive tool that allowed our customers to view and customize their home in full 3D. It included full support for desktop, mobile and tablet across all mainstream browsers. The BYOH platform was integral to our customer-facing processes, going on to receive significant positive attention after launch and was even featured by Forbes in July 2020.

I worked alongside a larger 3D team and was responsible for the full technical implementation including the customer-facing frontend, integrations within Unreal Engine/Pixel Streaming, devops, backend services, and Azure infrastructure and orchestration.

## Public-copy scope

The project-specific signalling integration module is retained under `Reference/SignallingIntegration/BYOH.js` to show how the application integrated with the external signalling layer. It is not a standalone signalling server.

Configuration values in `Host_Service/appsettings.json` use non-production example endpoints. A real deployment requires environment-specific Azure Key Vault, scaling-service, file-share, Unreal build, and signalling-server configuration.

## Stack

- C#/ASP.NET Core/.NET 6
- Windows Service hosting
- Azure Key Vault and Azure identity integration
- Azure Files integration
- GPU-aware process and session management
- Unreal Engine Pixel Streaming integration
- Automated build deployment
- Windows firewall automation
- Application Insights and health monitoring
- WiX installer project
- PowerShell provisioning
- Azure Pipelines
