# Licensing

[日本語](index-ja.md)

AIKernel.Providers is licensed under the Apache License 2.0.

## Why Apache 2.0

This repository contains provider implementations, endpoint integration,
runtime-driver boundaries, local runtime bridges, and native compute provider
surfaces. These are implementation surfaces rather than contract-only DTOs, so
Apache 2.0 provides the patent language needed for commercial and research use.

## Relationship to AIKernel.NET

AIKernel.NET contract packages are MIT licensed because they contain
interfaces, DTOs, enums, and schema contracts. AIKernel.Providers consumes those
contracts but does not change their license.

In short:

- AIKernel.NET contracts: MIT
- AIKernel.Core implementation packages: Apache-2.0 where applicable
- AIKernel.Providers implementation packages: Apache-2.0

## Provider Services and External Assets

AIKernel.Providers does not relicense external provider services, local model
weights, native CUDA libraries, OpenAI-compatible endpoints, Microsoft packages,
or local runtime assets.

Operators must use external services, model files, native binaries, and runtime
packages under their original license and service terms.

## Local Environment Files

Local environment files, path notes, API keys, and provider credentials are not
part of the package contract and must not be committed. Documentation may refer
to their purpose, but repository content must not depend on local absolute
paths or secrets.
