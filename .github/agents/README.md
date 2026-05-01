# Project Agent Specifications

This directory contains detailed specifications for specialized agents used in the AzerothCore Manager project.

## Available Agents

### 1. Frontend Specialist  
**File:** `frontend-specialist.md`  
**Focus:** React, Vite, TypeScript, UI/UX, Setup Wizards, Real-time UI  
**Use for:** Component development, wizard flows, real-time updates, state management, accessibility

## Backend Work

Backend work should use the standard Copilot cloud agent environment with the repository setup steps in
`.github/workflows/copilot-setup-steps.yml`. That keeps the .NET toolchain deterministic instead of relying on a
separate backend-specific agent profile.

## How to Use These Agents

When working with GitHub Copilot CLI or spawning specialized agents, reference these specifications to ensure consistent, expert-level assistance.

### Example: Spawning a Frontend Specialist

```
I need help creating the setup wizard UI.
Please act as the Frontend Specialist defined in .github/agents/frontend-specialist.md.

Context:
- Multi-step wizard (5 steps: name, database, ports, advanced, review)
- Use React Hook Form with Zod validation
- Store progress in localStorage
- Support back/next navigation with validation
```

## Agent Coordination

For features requiring both frontend and backend work:

1. **Define backend contracts in code first** - Use the standard cloud agent with the repository setup workflow.
2. **Then Frontend Specialist** - Implement UI based on those contracts.
3. **Iterate together** - Refine based on UX needs and technical constraints.

## Updating Agent Specs

As the project evolves, update these specifications to:
- Include new libraries or patterns adopted
- Document project-specific conventions
- Add lessons learned
- Reference implemented examples from the codebase

## Project Context

All agents should be aware of:
- **Project:** AzerothCore Manager - Web-based management for AzerothCore servers
- **Stack:** React + Vite (Frontend) + ASP.NET Core (Backend)
- **Architecture:** Docker-in-Docker pattern, managing AzerothCore container stacks
- **Key Integrations:** Docker.DotNet, SignalR, SOAP interface to AzerothCore
- **Documentation:** See `ARCHITECTURE_ANALYSIS.md` for complete technical details
