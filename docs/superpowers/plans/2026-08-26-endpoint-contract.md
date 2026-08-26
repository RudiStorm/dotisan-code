# Endpoint Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Formalize the first compile-time-oriented Dotisan endpoint contract without adding runtime reflection discovery.

**Architecture:** Dotisan.Core defines the marker, deterministic metadata, request/response handler boundary, and validator boundary without taking an ASP.NET dependency. Dotisan.AspNetCore continues to map endpoints explicitly through Minimal API route builders. The source generator will consume this contract in the following slice.

**Tech Stack:** .NET 8, C# static abstract interface members, records, ASP.NET Core routing, xUnit.

**Spec:** Dotisan Spec Kit.md, sections 8–10 and the endpoint-contract specification.

## Global Constraints

- Every endpoint MUST implement the endpoint marker contract.
- Request, response, validator, and handler types remain endpoint-local by convention.
- Endpoint metadata MUST be deterministic and explicit.
- Runtime reflection MUST NOT be required for normal endpoint discovery.
- Handlers and validators use ordinary DI-compatible interfaces.

### Task 1: Core Endpoint Contract

- [ ] Add a test endpoint with nested Request, Response, Handler, Validator, and static Configure metadata.
- [ ] Verify metadata normalizes tags and preserves route/method/permission fields.
- [ ] Verify a handler can be invoked through its generic interface.
- [ ] Implement the minimal Core contract.

### Task 2: ASP.NET Integration Boundary

- [ ] Add a test proving a typed endpoint definition can be created from an endpoint’s static metadata.
- [ ] Implement the explicit adapter without reflection or service-locator behavior.

### Task 3: Verification and Checkpoint

- [ ] Run the focused endpoint tests.
- [ ] Run the full solution build and tests.
- [ ] Commit the endpoint contract slice with a focused message.
