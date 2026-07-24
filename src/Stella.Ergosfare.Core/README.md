# Stella.Ergosfare.Core

Runtime core of [Ergosfare](https://github.com/stellayazilim/Ergosfare): the message
registry, pipeline-shape building, the mediator and its cached, reflection-free pipeline
executors, and pooled execution contexts.

This package is an implementation detail shared by the command/query/event modules.
Applications should reference the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package;
libraries that only declare handlers need
[`Stella.Ergosfare.Core.Abstractions`](https://www.nuget.org/packages/Stella.Ergosfare.Core.Abstractions).
