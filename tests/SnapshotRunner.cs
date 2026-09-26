// Writes the lifecycle snapshot: tests/.build/snap.exe <out.txt>. Compare with tests/lifecycle_baseline.txt.
public static class SnapshotRunner { public static int Main(string[] args) { return LifecycleSnapshot.SnapshotMain(args); } }
