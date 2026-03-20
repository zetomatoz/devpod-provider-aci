# ACI E2E Handoff And Next Paths

Snapshot date: 2026-03-16.

This note is a handoff for the next research or implementation thread. It combines:

- the current repo and prototype state
- the debugging history and live observations
- plain-language explanations of the shell and terminal issues
- three realistic next paths, in order

## Short Version

Most repo-side blockers are already fixed. The remaining blocker is narrower:

- DevPod's injection handshake now gets much further than before.
- The current ACI WebSocket prototype gets through `ping`, `pong`, and `done inject`.
- The failure now happens after that, when DevPod expects a clean raw STDIO tunnel for `helper ssh-server --stdio`.

Current working theory:

- Azure Container Instances exec is documented and observed as an interactive process or shell feature.
- DevPod's post-inject phase needs a byte-perfect transport.
- Those two models may simply not match well enough.

That does not prove the idea is impossible, but it does mean further shell escaping alone is unlikely to unlock the e2e.

## Where The Repo Stands

These earlier blockers were already fixed before this handoff:

- docs were corrected away from the invalid `--workspace` flag
- `WORKSPACE_IMAGE` was exposed and wired through the provider manifest
- the sample and e2e image was narrowed to `linux/amd64`
- Azure subscription registration for `Microsoft.ContainerInstance` was handled
- provider `status` output was cleaned up so it does not pollute DevPod's protocol

The current prototype work is in progress in these files:

- `src/DevPod.Provider.ACI/Services/AciService.cs`
- `src/DevPod.Provider.ACI/Commands/ExecCommand.cs`
- `src/DevPod.Provider.ACI/Services/IAciService.cs`
- `src/DevPod.Provider.ACI.Tests/AciServiceCommandTests.cs`
- `src/DevPod.Provider.ACI.Tests/CommandHandlersTests.cs`

What that prototype changed:

- switched from stuffing the whole command into a single ACI exec string to opening an ACI exec WebSocket session
- added an interactive execution path so the provider can proxy stdin and stdout instead of only returning a buffered result
- uploads the remote script to a temp file and then executes the temp file, instead of streaming the script itself over stdin
- added filtering for the shell bootstrap sentinel so DevPod's parser does not see internal setup noise
- added a raw passthrough mode for the `helper ssh-server --stdio` phase
- fixed a parsing bug where ACI binary frames were being treated as if they always had a stdout or stderr channel prefix

Focused tests passed in the last debugging round against this prototype:

- `AciServiceCommandTests`
- `CommandHandlersTests`
- `CreateCommandIntegrationTests`
- `ProviderManifestTests`

## What We Observed, In Order

1. The original command path hit real Azure limits.

- Large injected shell scripts eventually failed with `InvalidCommandLength`.
- That made the single giant `sh -c "..."` strategy a dead end.

2. Quoting and script-shape bugs were real, but they were not the final blocker.

- Earlier runs failed with errors like `Syntax error: Unterminated quoted string`.
- Those failures were improved by moving away from inline shell strings and into the exec WebSocket path.

3. Direct probes showed that plain `/bin/sh` under ACI behaves like a terminal session.

- commands were echoed back
- prompts such as `# ` appeared
- line endings came back as `\r\n`

This matched the official Azure CLI examples for ACI exec, which also show an interactive shell prompt and typed commands appearing in the output.

4. Trying to quiet that terminal behavior helped, but did not fully remove it.

- sending `stty -echo` reduced input echo in some cases
- setting `PS1=` tried to remove the prompt
- both were timing-sensitive and did not turn the session into a clean raw pipe

5. ACI WebSocket framing was not exactly what we first assumed.

- some binary frames were not prefixed with a stdout or stderr channel byte
- treating every binary frame as channel-prefixed corrupted output
- after fixing that, simple direct probes like `echo ping` became clean

6. One failure was caused by the DevPod script reading from stdin.

- DevPod's inject script includes `read`
- when we streamed the script itself through stdin, the script consumed later script lines instead of consuming the expected `pong`
- uploading the script to a temp file fixed that entire class of failure

7. After the temp-file fix, the handshake materially improved.

- the old `Received wrong answer for ping request ...` failure disappeared
- `devpod up` got through `done inject`
- that showed the protocol was no longer breaking during the early inject phase

8. The remaining failure is after injection, not during it.

- DevPod then waits for the agent to come up
- the run eventually ends with `context deadline exceeded`

That is why the remaining question is not "can we send the script?" but "can ACI exec carry the raw long-lived SSH-server STDIO stream that DevPod expects after the script is done?"

## Plain-Language Glossary

This section intentionally avoids terminal jargon.

### Shell

A shell is a command interpreter. It is the program that reads commands such as `ls`, `cd`, or `echo hi` and runs them.

Common shells on Linux are `sh` and `bash`.

### Terminal Session

A terminal session is the text console experience you normally see when you SSH into a machine or open a terminal window:

- you type commands
- the shell may echo your typing back to you
- it shows a prompt such as `$ ` or `# `
- it prints text output line by line

This is designed for humans.

### PTY

PTY means "pseudo-terminal". It is a software-made terminal.

Think of it as a virtual keyboard and screen. Programs behave differently when they think they are connected to a terminal instead of to a plain data pipe.

### STDIO

STDIO stands for:

- standard input: data sent into a program
- standard output: normal data printed by a program
- standard error: error data printed by a program

If two programs are connected over STDIO, one program can talk directly to the other through those byte streams.

### Binary-Clean

"Binary-clean" means the connection carries bytes exactly as they were sent.

Nothing extra is added:

- no prompt
- no command echo
- no line-ending rewriting
- no extra banner text

This matters because SSH and similar protocols are very strict. Even a few unexpected bytes can break the connection.

### CRLF

CRLF is a line ending made of two characters instead of one:

- `\r` means carriage return
- `\n` means newline

Many tools do not care. Some protocols do care.

## What "Terminal-Shaped" Means Here

When I say ACI exec looks "terminal-shaped", I mean it behaves like a remote interactive console, not like a plain invisible data hose.

A terminal-shaped connection tends to do human-oriented things:

- echo what you type
- print prompts
- treat lines specially
- rewrite line endings
- behave as though a person is sitting at a console

A binary-clean command pipe does not do those things. It just moves bytes.

That difference is exactly why the DevPod inject handshake can succeed, but the later SSH-server phase can still fail.

The handshake is mostly text. The SSH-server phase is a stricter long-lived byte stream.

## Why `stty -echo` And `PS1=` Were Not Enough

These commands try to make an interactive shell quieter:

- `stty -echo` asks the terminal not to print your typing back
- `PS1=` removes the visible shell prompt

That is useful, but it only changes some shell behavior. It does not prove the entire transport has become a raw byte pipe.

Why it was not enough in practice:

- the commands themselves must run after the shell starts, so there is already a timing window where startup text can leak
- removing the prompt does not remove all terminal semantics
- line ending behavior can still differ from a plain pipe
- the transport still appears to be built around terminal interaction, not around protocol-grade raw STDIO

Plain-language version:

- we managed to make the room quieter
- we did not turn the room into a sealed laboratory

## Can ACI Exec Be Made Binary-Clean Enough?

Short answer: maybe, but there is no good evidence yet, and the documented model argues against relying on it.

What Microsoft documents:

- ACI exec is mainly presented as a way to launch an interactive shell for troubleshooting.
- Microsoft explicitly says ACI exec supports launching a single process.
- Microsoft also says you cannot pass command arguments in the exec call.
- The REST API exposes `command`, `terminalSize`, `webSocketUri`, and `password`.

What is missing from the docs:

- no documented "raw mode"
- no documented "no PTY" or "disable terminal allocation" switch
- no documented framing contract for exact stdin or stdout byte behavior

What we observed live:

- prompt-like output and echoed commands appeared when using `/bin/sh`
- line endings came back as terminal-style text
- text-based inject steps now work
- the raw SSH-server step still times out

My current assessment:

- If ACI exec has a truly binary-clean mode, Microsoft does not document it clearly enough for this provider to trust it.
- Today, the safest engineering assumption is that ACI exec is fundamentally terminal-oriented until proven otherwise by a direct minimal proof-of-concept.

Important nuance:

- this is still an inference, not a formal Microsoft statement
- the docs do not literally say "ACI exec can never carry raw protocol traffic"
- they do strongly point toward a troubleshooting and interactive-shell design

## Can ACI Exec Run A Non-Shell Long-Lived Process?

Short answer: possibly yes for some cases, but that does not automatically solve the DevPod problem.

Why the answer is only "possibly":

- Microsoft says ACI exec can launch a single process
- a long-lived process is still "a single process"
- so, in principle, directly launching a binary should be possible

Why that still may not help enough:

- the API does not let us pass normal command arguments
- `helper ssh-server --stdio` needs arguments
- if we need a shell just to pass arguments, we reintroduce the terminal and quoting problems

The best version of this experiment would be:

- bake a tiny wrapper binary or wrapper script into the container image
- give it a simple no-argument path such as `/usr/local/bin/devpod-stdio`
- have that wrapper itself run the real `devpod helper ssh-server --stdio`
- call that wrapper directly through ACI exec

If that direct-wrapper experiment still shows prompt, echo, or stream corruption, it would be strong evidence that ACI exec is the wrong tunnel for this use case.

If it works, it may give a narrow path forward for ACI without redesigning the whole provider.

## The Three Next Paths

### Path 1: Time-Box The Current ACI Exec Approach

Goal: prove or disprove, quickly, whether ACI exec can carry DevPod's SSH-server STDIO if we bypass the shell as much as possible.

What to do:

- create a tiny wrapper executable or wrapper script inside the workspace image
- call that wrapper directly as the single exec command
- make the wrapper launch `devpod helper ssh-server --stdio`
- test the stream with a minimal raw proof-of-concept before involving full `devpod up`

Why this path exists:

- it is the cheapest way to answer the core technical question
- it uses the work already done in the current prototype
- it could preserve the overall ACI provider shape if it works

Stop condition:

- treat this as a short proof phase, not an open-ended debugging project
- if direct-wrapper exec still leaks terminal behavior or still times out, stop and move on

My recommendation:

- do this first
- keep it strictly time-boxed

### Path 2: Stay On ACI, But Stop Using Exec As The Main Tunnel

Goal: keep Azure Container Instances, but change the connection model.

What to do:

- start the required remote process at container startup instead of through `exec`
- or bake SSH and agent bootstrap into the workspace image
- connect over a real TCP port instead of trying to tunnel SSH over ACI exec STDIO

Why this path is plausible:

- ACI supports startup command override
- ACI supports init containers
- ACI supports public TCP ports on container groups

What this would likely mean in practice:

- the workspace image becomes more specialized
- the provider may need to behave more like an SSH-oriented provider
- authentication and security design become more important

Tradeoffs:

- more image and runtime design work
- likely more predictable than fighting a terminal-shaped exec tunnel
- could still stay within the ACI product boundary if AKS is undesirable

My recommendation:

- this is the best "stay on ACI" fallback if Path 1 fails

### Path 3: Pivot To A Kubernetes-Style Non-Machine Provider

Goal: stop fighting ACI's tunnel model and move to a control plane that matches DevPod's non-machine design better.

What to do:

- evaluate AKS or another Kubernetes target instead of direct ACI
- model the provider after DevPod's Kubernetes provider patterns
- let the Kubernetes control plane be the transport and lifecycle layer

Why this path is attractive:

- DevPod already has a Kubernetes provider
- DevPod explicitly supports non-machine providers such as Kubernetes, SSH, and Docker
- Kubernetes is a more natural home for exec, init containers, sidecars, and container lifecycle customization

Tradeoffs:

- bigger product shift
- more infrastructure cost and complexity than plain ACI
- much stronger fit if the goal is broad DevPod compatibility instead of a narrow image-only ACI experiment

My recommendation:

- this is the strongest long-term path if you want a first-class DevPod backend rather than an ACI-specific demo

## Suggested Decision Order

If the goal is to avoid wasted time, I would make decisions in this order:

1. Run the direct-wrapper experiment from Path 1.
2. If Path 1 fails, decide whether "must stay on ACI" is still a hard requirement.
3. If ACI is still required, move to Path 2.
4. If broad DevPod compatibility matters more than ACI purity, move to Path 3.

## Why Chunking Was Probably The Wrong Direction

Chunking helped with command length, but it did not solve the transport mismatch.

The issue is no longer mainly "how do we send a big script?" The issue is "what kind of tunnel is ACI exec actually giving us?"

If the tunnel itself is terminal-shaped, chunking a script into smaller pieces does not fix the later raw STDIO phase.

That is why the architecture question became more important than the string-length question.

## Specific DevPod Agent Notes

From DevPod's current source and docs:

- the local CLI runs an inject script remotely
- that script prints `ping`
- the local side replies with `pong`
- the script may ask for a binary upload using `ARM-true` or `ARM-false`
- when injection is complete, it prints `done`
- after that, DevPod switches from a text handshake into a raw STDIO pipe
- DevPod uses that STDIO tunnel to run an SSH server over the tunnel

Why that matters:

- the early phase is mostly simple line-based text
- the late phase is not
- that is exactly why partial progress can be misleading

A provider can appear "almost working" because `ping` and `done` succeed, while the real connection still fails immediately afterward.

## External Sources

Official Microsoft Learn pages checked on 2026-03-16:

- ACI exec: [Execute a command in a running Azure container instance](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-exec)
- ACI exec REST API: [Containers - Execute Command](https://learn.microsoft.com/en-us/rest/api/container-instances/containers/execute-command?view=rest-container-instances-2025-09-01)
- ACI startup command override: [Set the command line in a container instance](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-start-command)
- ACI init containers: [Run an init container for setup tasks in a container group](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-init-container)
- ACI public IP and TCP port examples: [Quickstart: Deploy a container instance in Azure using an ARM template](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-quickstart-template)

Official DevPod docs and source checked on 2026-03-16:

- DevPod architecture overview: [How it works](https://devpod.sh/docs/how-it-works/overview)
- DevPod provider agent docs: [Provider Agent](https://devpod.sh/docs/developing-providers/agent)
- DevPod provider types: [What are Providers?](https://devpod.sh/docs/managing-providers/what-are-providers)
- DevPod inject wrapper source: [pkg/agent/inject.go](https://raw.githubusercontent.com/loft-sh/devpod/main/pkg/agent/inject.go)
- DevPod inject transport source: [pkg/inject/inject.go](https://raw.githubusercontent.com/loft-sh/devpod/main/pkg/inject/inject.go)
- DevPod inject script: [pkg/inject/inject.sh](https://raw.githubusercontent.com/loft-sh/devpod/main/pkg/inject/inject.sh)
- DevPod Kubernetes provider: [loft-sh/devpod-provider-kubernetes](https://github.com/loft-sh/devpod-provider-kubernetes)

## Repo Files Worth Reading First In The Next Thread

- `src/DevPod.Provider.ACI/Services/AciService.cs`
- `src/DevPod.Provider.ACI/Commands/ExecCommand.cs`
- `src/DevPod.Provider.ACI/Services/IAciService.cs`
- `src/DevPod.Provider.ACI.Tests/AciServiceCommandTests.cs`
- `docs/command-execution-flow.md`
- `tests/e2e/README.md`

## Recommended Opening Prompt For The Next Thread

If you want a clean restart, a useful next-thread prompt would be:

> Read `docs/aci-e2e-handoff-2026-03-16.md`, inspect the current prototype in `AciService.cs`, and either:
> 1. design the smallest possible direct-wrapper proof for ACI exec raw STDIO, or
> 2. propose a concrete redesign for an ACI startup-command or SSH-port model, or
> 3. compare that against a Kubernetes non-machine provider approach.

