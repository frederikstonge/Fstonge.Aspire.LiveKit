I want you to remove eveerything in Fstonge.Aspire.Hosting.LiveKit.Tests and start over.

Try to make a reusable framework for the distributed application builder that can be reused across multiple test files.

Each file in Fstonge.Aspire.Hosting.Livekit should have its own test file (unless testing it is useless)

The current tests are working invididually, but hangs when ran in parallels (the ones with a distributed application builder, all tests for egress configuration works in parallel)