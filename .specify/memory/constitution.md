<!--
Sync Impact Report
Version: none → 1.0.0
Modified principles: placeholder template → Samsung-inspired voice recorder principles
Added sections: Design Constraints, Development Workflow
Removed sections: placeholder guidance comments
-->

# NajGravador Constitution

## Core Principles

### I. Samsung-Inspired User Experience
NajGravador MUST deliver a clean, intuitive recording experience inspired by Samsung voice recorder behavior.
- Recording controls are always easy to access, with clear start, pause, resume, save, cancel, and playback actions.
- The app MUST present recording status and elapsed time prominently, matching the expectation of a polished voice recorder.
- User flows MUST feel responsive and predictable, with no silent failures or confusing state transitions.

### II. Reliable Recording Lifecycle
Every recording session MUST be managed as a first-class lifecycle.
- Start, pause, resume, finalize, and cancel operations MUST be implemented consistently.
- Paused recordings MUST be available for review before final save, and cancellations MUST discard temporary data cleanly.
- The app MUST never leave an active recording running after navigation away or deletion.

### III. Stable Playback and Management
Playback and recording management MUST be robust and fault-tolerant.
- Only one audio playback session MAY run at a time.
- Playback controls MUST toggle correctly between play, pause, stop, and reset states.
- Deleting a recording MUST immediately stop any active playback and remove the file safely.

### IV. Simple, Trustworthy Persistence
Recordings MUST be stored with durable metadata and accessible files.
- Saved recordings MUST include name, timestamp, duration, and local file path.
- Temporary preview files and segment artifacts MUST be cleaned up after finalize or cancel.
- Persistence MUST never corrupt existing recordings during merges or state changes.

### V. MAUI-First Implementation Standards
NajGravador MUST use MAUI best practices for cross-platform audio behavior.
- UI bindings and navigation MUST remain declarative and maintainable.
- Service abstractions MUST separate recording logic from page UI logic.
- Error handling MUST be explicit and surfaced to users rather than failing silently.

## Design Constraints
NajGravador MUST remain faithful to the voice recorder metaphor while using the existing MAUI app architecture.
- Feature decisions MUST prioritize the Samsung recorder expectations for recording visibility, pause/review, and playback controls.
- The app MUST keep audio files in app-local storage and avoid external cloud dependencies for core recording functionality.
- The implementation MUST support both immediate preview and final save without exposing raw temporary segment files to the user.

## Development Workflow
NajGravador development MUST follow a lightweight quality workflow with clear review and validation steps.
- Changes to recording or playback behavior MUST include a regression check for pause/resume, finalize, cancel, and delete flows.
- Any UI or audio lifecycle change MUST be verified on at least one MAUI target platform before merge.
- Bug fixes MUST preserve the app’s Samsung-inspired recorder behavior rather than introducing unexpected alternate flows.

## Governance
This constitution governs feature and implementation decisions for NajGravador. All recording and playback changes MUST be evaluated against this document.
- Amendments require explicit documentation in the constitution file and a version bump.
- Any change that alters the defined recording lifecycle or playback contract MUST be reviewed and approved before merging.
- Compliance review MUST occur on every pull request affecting audio recording, playback, persistence, or UI state.

**Version**: 1.0.0 | **Ratified**: 2026-08-06 | **Last Amended**: 2026-08-06
