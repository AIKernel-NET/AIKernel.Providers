# Concept Elevation Notes / 概念昇格ノート

## Canonical Reference / 正典参照

Common naming rules are maintained in AIKernel.NET:

- `AIKernel.NET/docs/canonical-language/index.md`
- `AIKernel.NET/docs/design/concept-elevation-refactoring-design.md`
- `AIKernel.NET/docs/guidelines/concept-elevation-guidelines.md`
- `AIKernel.NET/docs/migration/concept-elevation-v0.1.1.1.md`
- `AIKernel.NET/docs/todo/concept-elevation-refactoring-todo.md`

Provider-specific dependency boundaries remain defined by:

- `docs/guidelines/provider-development-guidelines.md`
- `docs/guidelines/dependency-boundary-checklist.md`

## Providers Scope / Providers の対象範囲

AIKernel.Providers owns provider substrate, manifest-driven providers, and
runtime-configurable capability adapters. It does not own Gate decisions.

AIKernel.Providers は provider substrate、manifest-driven provider、
runtime-configurable capability adapter を担当します。Gate decision は担当しません。

Added concept surfaces:

- `AIKernel.Providers.Council.Concepts.EthosCouncil`
- `AIKernel.Providers.Council.Concepts.PathosSignal`
- `AIKernel.Providers.Council.Concepts.LogosVerifier`
- `AIKernel.Providers.Council.Concepts.AisthesisRouter`
- `AIKernel.Providers.Council.Concepts.PhantasiaScene`

## Compatibility Note / 互換性メモ

The existing `EthosSemanticEvaluationProvider`,
`PathosSemanticEvaluationProvider`, and `LogosSemanticEvaluationProvider` names
remain for public compatibility. They are documented compatibility exceptions
in the architecture naming test and must not be used as precedent for new
provider implementation names.

既存の `EthosSemanticEvaluationProvider`、`PathosSemanticEvaluationProvider`、
`LogosSemanticEvaluationProvider` は public compatibility のため維持します。
architecture naming test では互換例外として扱い、新規 provider implementation
名の前例にはしません。

## Guardrails / 境界ルール

- Council providers return semantic material and diagnostics only.
- Providers do not return `GateDecisionKind`, `TrajectoryGateDecisionKind`, or
  `RejectReasonKind`.
- Providers do not construct `GateInput`.
- Concept names do not appear on DTO, mapper, adapter, serializer, converter,
  manifest schema, or provider implementation names.

## Tests / テスト

`tests/Dependency/AIKernel.Providers.DependencyGuard.Tests/ConceptElevationNamingTests.cs`
guards new naming violations while allowing the documented compatibility
provider names.
