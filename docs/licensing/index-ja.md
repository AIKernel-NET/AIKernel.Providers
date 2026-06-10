# Licensing

[English](index.md)

AIKernel.Providers は Apache License 2.0 で提供されます。

## Apache 2.0 を採用する理由

この repository は Provider implementation、endpoint integration、
runtime-driver boundary、local runtime bridge、native compute Provider surface
を含みます。これらは contract-only DTO ではなく implementation surface であるため、
commercial / research use に必要な patent language を持つ Apache 2.0 を採用します。

## AIKernel.NET との関係

AIKernel.NET contract package は interface、DTO、enum、schema contract を含むため
MIT licensed です。AIKernel.Providers はそれらの contract を利用しますが、
license は変更しません。

要約:

- AIKernel.NET contracts: MIT
- AIKernel.Core implementation package: 必要に応じて Apache-2.0
- AIKernel.Providers implementation package: Apache-2.0

## Provider Service と外部 Asset

AIKernel.Providers は external provider service、local model weight、native CUDA
library、OpenAI-compatible endpoint、Microsoft package、local runtime asset を
再ライセンスしません。

operator は外部 service、model file、native binary、runtime package を、それぞれ
元の license と service terms に従って使用する必要があります。

## Local Environment File

local environment file、path note、API key、provider credential は package contract
の一部ではなく、commit してはいけません。documentation はそれらの目的に触れる
ことがありますが、repository content は local absolute path や secret に依存しては
いけません。
