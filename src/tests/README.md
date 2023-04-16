# Tests

Tests require installed game to run. Please note this:

1. Some tests output warnings but pass nevertheless because they just detect bad offsets and other anomalies
2. Tests iterate over all `vpp_pc` files automatically
3. There is a test called `UnpackNested`. It does not run with other tests (marked as `Explicit`). When run manually, it unpacks all vpp_pc contents inside to `game/data/.syncfaction/packer_tests` (writing 30gb!)
4. If you have run `UnpackNested` and have unpacked data, tests will also iterate over all extracted `str2_pc` files
