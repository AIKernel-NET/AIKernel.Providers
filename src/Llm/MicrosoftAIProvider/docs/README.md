# AIKernel.Providers.MicrosoftAI

[日本語](README-ja.md)

AIKernel.Providers.MicrosoftAI adapts Microsoft.Extensions.AI based model
execution to the AIKernel provider model.

## Role

This provider was moved from AIKernel.Core into AIKernel.Providers management
for the 0.1.1 public release.

The migration keeps Core focused on contracts and runtime semantics while this
repository owns the Microsoft.Extensions.AI implementation package, tests,
documentation, and Python wrapper inclusion.

## Public Surface

The package includes:

- `OpenAICompatibleProvider`
- `OpenAICompatibleProviderOptions`
- `OpenAICompatibleProviderCapabilities`
- `OpenAICompatibleCredential`
- `OpenAICompatibleResponseMapper`
- provider execution exception types
- dependency-injection hosting extensions

## Runtime Behavior

The provider accepts Microsoft.Extensions.AI `IChatClient` implementations and
projects responses into AIKernel provider results. It preserves AIKernel's
provider capability contract while allowing OpenAI-compatible model execution.

## Python Inclusion

The Python package `aikernel-providers` bundles
`AIKernel.Providers.MicrosoftAI.dll` and exposes `MicrosoftAIProviderOptions`.
This follows the ownership move from Core to Providers.

## Dependency Boundary

MicrosoftAIProvider may use Microsoft.Extensions.AI and AIKernel contract
packages. It must not depend on AIKernel.Tools.
