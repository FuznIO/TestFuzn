# Copilot Instructions

## Project Guidelines
- In the TestFuzn framework, GlobalState is intended for external (consumer) use, not internal use. Internally, TestSession.Current should be used directly instead of going through GlobalState.