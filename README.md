# Mycelium

> Let every page feed the network.

Mycelium is an experimental, extensible network crawler, service-discovery scanner, and protocol-analysis toolkit built with .NET.

Today, Mycelium can safely fetch a single HTTP or HTTPS resource, pass it through an observable plugin pipeline, and discover links from HTML documents.

The long-term goal is much broader:

- Crawl modern and legacy resource networks.
- Discover services even when they are running on unexpected ports.
- Identify protocols from observed behavior rather than port numbers alone.
- Probe known services under strict limits.
- Evaluate selected RFC requirements using reproducible wire evidence.

Mycelium is in early development. It is not yet a recursive crawler, a general-purpose scanner, or a complete RFC conformance suite.

---

## Why Mycelium?

Most crawlers stop at HTTP.

Most scanners stop at open ports.

Most service detectors trust banners, well-known ports, or brittle fingerprints.

Mycelium is intended to treat the network as a graph of resources, services, and observable behavior.

An Echo server is still an Echo server if it runs on port `7777`.

A Gopher server is still Gopher if it runs on port `7000`.

A Daytime service should be recognizable because of how it behaves, not merely because it happens to listen on port `13`.

Mycelium should eventually answer questions such as:

- What resources can be discovered from this starting point?
- What services are running on this host or network?
- What protocol is actually speaking behind this open port?
- Does this service behave consistently with the tested requirements of its RFC?
- What exact bytes and connection behavior support that conclusion?

The project is being designed around four related but distinct capabilities:

1. **Resource crawling**
2. **Protocol probing**
3. **Port-agnostic service discovery**
4. **Evidence-based conformance testing**

They can share networking, limits, logging, URI handling, storage, and reporting, but they should remain separate concepts.

---

# Current Status

Mycelium currently provides a small but functional HTTP fetch-and-analysis foundation.

## Implemented

- A .NET 10 CLI host.
- Dependency injection and configuration.
- Structured console logging.
- An extensible crawl-plugin contract.
- An observable plugin pipeline that:
  - Runs plugins in registration order.
  - Aggregates discovered URLs and findings.
  - Propagates caller cancellation.
  - Logs plugin execution.
  - Isolates plugin failures so one broken plugin does not stop the remaining plugins.
- A bounded HTTP/HTTPS fetcher with:
  - Configurable overall timeout.
  - Manual redirect handling.
  - Redirect limits.
  - Streamed response reading.
  - Declared response-size validation.
  - Actual response-size validation while streaming.
  - Cancellation support.
  - Automatic decompression.
  - Conservative text decoding.
  - Structured completion and failure logging.
- An HTML link-discovery plugin powered by AngleSharp.
- HTML discovery support for:
  - `<a href>`
  - `<area href>`
  - `<iframe src>`
  - `<link href>`
  - `<base href>` resolution
  - Relative URL resolution
  - Relationship and nearby text context
  - Filtering of fragments and unsupported schemes
- Automated tests covering:
  - Text and binary HTTP responses.
  - Oversized-response rejection.
  - Relative redirects.
  - Plugin ordering and aggregation.
  - Plugin-failure isolation.
  - Caller cancellation.
  - HTML link discovery.
  - HTML `<base>` handling.
  - Non-HTML content filtering.

## Not Implemented Yet

- Recursive crawling.
- A crawl frontier or queue.
- Visited-resource deduplication.
- Crawl-depth and resource-count enforcement.
- Host, domain, network, or scheme scope policies.
- Per-host and global concurrency controls.
- Retry policies.
- `robots.txt` behavior.
- Browser rendering.
- Runtime plugin discovery.
- Gopher fetching or menu discovery.
- Finger, Echo, Daytime, or QOTD probes.
- Port scanning.
- Port-agnostic service recognition.
- Internet-scale scanning.
- RFC conformance rules.
- Persistent crawl or scan storage.
- Structured export beyond current console output.

The current CLI intentionally fetches one resource at a time.

It is a foundation for crawling and scanning, not yet the finished organism.

---

# Command-Line Usage

## Requirements

- .NET 10 SDK

## Build

```bash
dotnet build Mycelium.slnx
```

## Test

```bash
dotnet test Mycelium.slnx
```

## Show Help

```bash
dotnet run --project Mycelium.Cli -- help
```

## List Registered Plugins

```bash
dotnet run --project Mycelium.Cli -- plugins list
```

## Fetch and Analyze One HTTP Resource

```bash
dotnet run --project Mycelium.Cli -- fetch https://example.com/
```

The fetch command currently prints a summary containing:

- Requested URL
- Final URL
- HTTP status
- Content type
- Byte count
- Whether text was decoded
- Duration
- Discovered URL count
- Finding count

---

# Configuration

HTTP fetching is configured through `Mycelium.Cli/appsettings.json`.

```json
{
  "HttpFetch": {
    "TimeoutSeconds": 30,
    "MaxResponseBytes": 4194304,
    "MaxRedirects": 10,
    "UserAgent": "Mycelium/0.1"
  }
}
```

These limits are intentional.

Mycelium should remain bounded by default, even when pointed at malformed, hostile, strange, or simply ridiculous services.

---

# Solution Structure

```text
Mycelium.Cli/
    CLI host, command handling, configuration, and composition root

Mycelium.Contracts/
    Shared crawl requests, fetched-resource contracts, plugin contracts,
    discovered resources, findings, and rendering requests

Mycelium.Core/
    Fetching, plugin-pipeline execution, options, logging,
    routing, scanning, and dependency-injection registration

Mycelium.Plugins.HtmlLinks/
    AngleSharp-based HTML link discovery

Mycelium.Test/
    Fetcher, pipeline, plugin, scanner, and protocol tests
```

Plugins are currently modular at compile time.

The CLI directly references and registers the HTML plugin. Runtime DLL discovery is not implemented and is not needed for the next development stages.

---

# Architecture Today

The current execution path is:

```text
CLI fetch command
    -> CrawlRequest
    -> HTTP page fetcher
    -> CrawlDocument
    -> crawl plugin pipeline
    -> discovered URLs and findings
    -> console summary
```

This works for HTTP, but the shared contracts are still HTTP-shaped.

`CrawlDocument` currently requires:

- `HttpStatusCode`
- HTTP-style headers
- Content type
- HTTP/browser-oriented fetch mode

Those concepts do not naturally describe:

- Gopher menus
- Finger responses
- Echo exchanges
- Daytime text
- QOTD responses
- Arbitrary protocol transcripts

The next architectural step is to replace the HTTP-shaped fetched-document model with a protocol-neutral fetched-resource model.

The existing HTTP implementation should be adapted, not rewritten. Its timeouts, redirect handling, streamed reads, size limits, decoding, cancellation, and logging are already useful and should survive unchanged.

---

# Grand Vision

## 1. Multi-Protocol Resource Crawling

Mycelium should retrieve resources through protocol-specific fetchers selected by URI scheme or target type.

Initial targets:

- HTTP
- HTTPS
- Gopher

Possible later targets:

- Gemini
- Finger
- FTP
- Other protocols that expose linkable or retrievable resources

Gopher is the ideal second protocol because it provides both:

- Fetchable content
- Discoverable links that form a graph

Supporting Gopher will prove whether Mycelium is actually protocol-neutral or merely an HTTP crawler wearing an old-protocol hat.

A future crawl frontier should track:

- Queued resources
- Visited resources
- Canonicalized resources
- Referrer
- Crawl depth
- Maximum resource count
- Allowed schemes
- Allowed hosts or domains
- Per-host concurrency
- Global concurrency
- Retry policy
- Crawl delay

The first crawler should be single-threaded.

Correct scope, canonicalization, and deduplication matter more than speed.

---

## 2. Protocol Probing

Not every protocol forms a crawlable graph.

Services such as Echo, Daytime, QOTD, and many Finger endpoints are better treated as probe targets.

A protocol probe deliberately:

1. Connects to a known endpoint.
2. Sends controlled input when the protocol requires it.
3. Reads a bounded response.
4. Records the exact exchange.
5. Produces structured observations.

Possible probe targets include:

- Echo
- Daytime
- QOTD
- Finger
- Gopher
- Other selected historical TCP or UDP services

A probe is not a crawler.

A crawler follows discovered resources.

A probe tests a known target.

They can share network and safety infrastructure without sharing the same abstraction.

---

## 3. Port-Agnostic Service Discovery

Mycelium should eventually discover recognizable services regardless of which port they use.

Traditional scanners usually answer:

> Which ports are open?

Mycelium should attempt to answer:

> What service or protocol is actually running behind this endpoint?

Recognition should be based on observable behavior whenever possible.

### Echo Example

An Echo recognizer could:

1. Open a TCP connection or send a UDP datagram.
2. Generate a unique random byte sequence.
3. Send those bytes.
4. Read the response under a strict timeout and byte limit.
5. Compare the returned bytes with the original payload.
6. Report a probable Echo service when the response matches exactly.

Example:

```text
Connect: 203.0.113.10:7777
Send:    7B A1 04 D9 55 2C
Receive: 7B A1 04 D9 55 2C

FOUND: probable Echo service
Transport: TCP
Confidence: high
Evidence: response exactly matched the generated probe payload
```

Or, more honestly enthusiastic:

```text
FOUND AN ECHO SERVER — YAY
```

### Other Recognizer Examples

#### Daytime

- Connect without sending data.
- Observe whether readable date or time text is returned.
- Observe whether the connection closes.
- Compare the response against likely Daytime behavior.

#### QOTD

- Connect without sending data.
- Read a bounded text response.
- Observe whether the server closes the connection.
- Avoid assuming every short text response is QOTD.

#### Finger

- Send a carefully bounded query.
- Inspect line-oriented output.
- Record termination behavior.
- Distinguish useful evidence from generic text banners.

#### Gopher

- Send a selector.
- Inspect whether the result resembles a Gopher menu or resource.
- Validate menu rows without crashing on malformed input.
- Discover linked Gopher resources.

#### Echo

- Send unpredictable bytes.
- Require an exact unchanged response.
- Use multiple probe payloads where additional confidence is needed.

### Discovery Results

Each result should include:

- Target address
- Port
- Transport
- Candidate protocol
- Confidence
- Recognizer
- Bytes sent
- Bytes received
- Connection behavior
- Duration
- Human-readable reasoning
- Competing or ambiguous matches

Example:

```text
FOUND probable Echo service

Target:      203.0.113.10:7777
Transport:   TCP
Confidence:  High
Recognizer:  echo.round-trip-exact-match
Evidence:    32 unpredictable bytes were returned unchanged
Duration:    18.4 ms
```

### Scan Scope

The scanner should eventually support:

1. A single endpoint
2. A host and selected ports
3. A subnet
4. User-defined network ranges
5. Carefully controlled Internet-scale research scans

Internet-scale scanning is a later goal and must be explicit.

It should never be the accidental result of running a default command.

Large scans should support:

- Conservative global rate limits
- Conservative per-host rate limits
- Small probe payloads
- Strict connection timeouts
- Strict response limits
- Scan resumability
- Checkpointing
- Deduplication
- Exclusion lists
- Scope recording
- Configuration recording
- Opt-out handling
- Clear operator identification where appropriate
- No exploitation
- No authentication attempts
- No state-changing commands

The long-term objective is a searchable map of observable services, including forgotten implementations running on unexpected ports.

---

## 4. Evidence-Based Protocol Conformance

Mycelium should eventually evaluate selected observable requirements from RFCs and protocol specifications.

The key word is **observable**.

A remote probe can show that a service behaved consistently with the requirements exercised by the probe.

It cannot prove that every code path or every requirement is fully compliant.

Results should say:

```text
Observed conformance with the tested requirements.
```

They should not say:

```text
This server is fully RFC compliant.
```

Each conformance result should retain:

- Target
- Protocol
- Rule identifier
- Specification reference
- Bytes sent
- Bytes received
- Connection termination
- Duration
- Error, if any
- Pass, warning, failure, or not-tested result
- Human-readable reasoning

Example:

```text
PASS RFC867-RESPONSE-WITHOUT-REQUEST
PASS RFC867-CONNECTION-CLOSED
WARN RFC867-RESPONSE-FORMAT-IMPLEMENTATION-DEFINED
FAIL RESPONSE-EXCEEDED-CONFIGURED-LIMIT
```

Rules should evaluate retained transcripts.

They should not hide network activity inside rule implementations.

That separation keeps the evidence reusable and the rule logic testable.

---

# The Happy Protocol Ecosystem

Mycelium should work naturally with the JoyfulReaper Happy protocol servers.

Potential known-good targets include:

- HappyEcho
- HappyDaytime
- HappyQOTD
- HappyGopher
- HappyFinger

These projects can form a controlled protocol laboratory.

For each supported protocol, Mycelium should be tested against:

1. A known-good Happy implementation.
2. Intentionally broken in-process fixtures.

Example broken fixtures:

- A Daytime server that waits for client input.
- A QOTD server that never closes.
- A Gopher menu with missing or malformed fields.
- A Finger server with incorrect line termination.
- An Echo server that modifies returned bytes.
- An Echo server that returns only part of the payload.
- A service that sends an oversized response.
- A service that accepts a connection but never responds.

A useful protocol test must prove both sides:

- The valid implementation passes.
- The broken fixture fails for the expected reason.
- The exact transcript is available as evidence.

This can turn several small retro-protocol projects into one coherent ecosystem:

- Happy servers implement protocols.
- Mycelium discovers them.
- Mycelium probes them.
- Mycelium evaluates selected requirements.
- Broken fixtures prove the rules work.

---

# Core Design Principles

## Protocol Neutrality

Shared contracts must describe resources, endpoints, observations, and transcripts without inventing fake HTTP values for non-HTTP protocols.

## Bounded by Default

Every network operation should have explicit limits for:

- Time
- Bytes
- Redirects
- Connections
- Concurrency
- Retries

## Behavior Over Port Numbers

Ports are hints.

Observed protocol behavior is stronger evidence.

## Evidence Over Assertions

Findings should retain enough information to explain exactly why they were produced.

## Confidence, Not False Certainty

Protocol detection is often probabilistic.

Results should support confidence levels and ambiguity instead of pretending every match is absolute.

## Failure Isolation

One malformed resource, failed plugin, broken endpoint, or incorrect recognizer should not destroy an otherwise valid run.

## Small Vertical Slices

Add one end-to-end capability at a time.

Do not build a universal crawler and scanner framework before a second protocol proves what the abstraction actually needs.

## Honest Status

Documentation and command names should describe what Mycelium does today, not merely what the roadmap hopes it will eventually do.

---

# Safety and Scope

Mycelium currently operates as a local CLI under the control of its user.

That matters.

If Mycelium later accepts arbitrary targets through a public API, dashboard, or hosted service, target validation must defend against server-side request forgery and unsafe network access.

Hosted-use protections should include:

- Loopback restrictions
- Private-network restrictions
- Link-local restrictions
- Validation after DNS resolution
- Redirect-destination validation
- DNS-rebinding defenses
- Port restrictions
- Authentication
- Authorization
- Rate limiting
- Audit logging

These restrictions are not necessarily appropriate for every local CLI use case.

They become mandatory when an untrusted user can choose the target.

Internet-scale scanning must also be deliberate, lawful, documented, and conservative.

---

# Development Roadmap

## Phase 0 — HTTP Fetch-and-Analyze Foundation

Current state:

- CLI host
- Bounded HTTP fetching
- Plugin pipeline
- HTML link discovery
- Tests

## Phase 1 — Protocol-Neutral Fetching

- Replace the HTTP-shaped fetched-document model.
- Introduce a protocol-neutral fetched-resource model.
- Replace `IPageFetcher` with a resource-fetcher abstraction.
- Adapt the current HTTP implementation without changing behavior.
- Update plugins and the CLI.
- Keep all current tests green.

## Phase 2 — Fetcher Routing

- Add resource-fetcher selection.
- Route by supported URI scheme.
- Add clear unsupported-scheme errors.
- Test multiple registered fetchers.
- Avoid service-locator-style ambiguity.

## Phase 3 — Gopher Vertical Slice

- Add `gopher://` URI support.
- Implement a bounded Gopher resource fetcher.
- Preserve raw response bytes.
- Decode text conservatively.
- Add a Gopher menu discovery plugin.
- Convert valid menu rows into discovered Gopher URIs.
- Report malformed rows as findings.
- Test against an in-process Gopher server.

Target command:

```text
mycelium fetch gopher://host:70/1/
```

## Phase 4 — Actual Crawling

- Add a crawl frontier.
- Add canonicalization.
- Add deduplication.
- Add depth limits.
- Add resource limits.
- Add scheme and host scope policies.
- Start single-threaded.
- Add concurrency only after correctness is established.

Target command:

```text
mycelium crawl gopher://host/1/ --max-depth 2 --max-resources 100
```

## Phase 5 — Protocol Probe Infrastructure

- Introduce probe-target contracts.
- Introduce transcript contracts.
- Preserve sent and received bytes.
- Add strict timeout and response limits.
- Add Echo, Daytime, QOTD, and Finger probes.
- Produce structured observations.

Possible commands:

```text
mycelium probe echo 203.0.113.10:7777 --tcp
mycelium probe daytime 203.0.113.10:1313
mycelium probe finger example.net:79
```

## Phase 6 — Port-Agnostic Recognition

- Add protocol recognizers.
- Scan selected ports on one host.
- Run bounded recognizers against open endpoints.
- Add confidence scoring.
- Report ambiguous matches.
- Add exact evidence to findings.

Possible command:

```text
mycelium identify 203.0.113.10 --ports 1-1024,7000,7777
```

## Phase 7 — Network-Range Scanning

- Add subnet support.
- Add user-defined ranges.
- Add checkpoints and resumability.
- Add exclusion lists.
- Add rate limits.
- Add scan-state persistence.
- Add structured export.

Possible command:

```text
mycelium scan 203.0.113.0/24 --ports 1-1024 --profile legacy-safe
```

## Phase 8 — Conformance Rules

- Add independently testable rules.
- Associate rules with specification references.
- Evaluate retained transcripts.
- Add known-good and intentionally broken fixtures.
- Export evidence-backed reports.

## Phase 9 — Internet-Scale Research Scanning

Long-term only:

- Distributed scan coordination
- Durable scan queues
- Result deduplication
- Network ownership and exclusion handling
- Operator identification
- Opt-out processing
- Conservative probe profiles
- Searchable service catalog
- Historical comparison of observed behavior

This phase should happen only after the single-host and network-range tooling is stable, bounded, auditable, and boring.

---

# Next Development Slice

The next coding slice should make fetching protocol-neutral without changing current HTTP behavior.

Recommended sequence:

1. Introduce `FetchedResource`.
2. Remove the HTTP/browser `FetchMode` from the generic request.
3. Represent protocol status as optional protocol-specific data.
4. Replace HTTP-specific headers with generic metadata.
5. Replace `IPageFetcher` with `IResourceFetcher`.
6. Adapt `HttpPageFetcher` into `HttpResourceFetcher`.
7. Update the plugin pipeline to consume `FetchedResource`.
8. Update the HTML plugin.
9. Update CLI output.
10. Keep all current tests green.

Do not add Gopher in the same commit.

Do not add the scanner in the same commit.

The purpose of the first slice is simple:

- The current HTTP behavior remains unchanged.
- Shared contracts stop lying about non-HTTP protocols.
- The architecture becomes ready for a second fetcher.

Once that checkpoint is clean:

1. Add fetcher routing.
2. Add Gopher fetching.
3. Add Gopher menu discovery.
4. Then begin actual crawling.
5. Build protocol probes after the resource path is stable.
6. Build port-agnostic identification on top of retained probe transcripts.

---

# Project Status

Mycelium is experimental and under active development.

The foundation is intentionally small.

The goal is not to predict every protocol abstraction in advance.

The goal is to build one bounded, testable vertical slice at a time until the network begins feeding the network.
