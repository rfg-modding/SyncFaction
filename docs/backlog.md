# Backlog

Ideas, notes about features, testing, release process, etc

## Release

* FF: coordinate with Goober
  * terraform rebuild
    * nuke mod id 7695
    * maybe wait for Camo to include new map or fixes
    * approve 1.0751
  * rename FF categories (early september)
  * remove old updates from search
  * delist old patches in FF?
* remove stuff from news page: links dont work, formatting is bad, a lot of content is bad
* test live update from FF on both CLEAN steam and gog versions, including save transfer, including RSL (should it be pubilc?)
* publish release .exe on GH and FF

## Backlog

* fix jumping text on devmode toggle
* port/network checker?
  * inspect how game uses network for client and host modes
  * check if traffic can flow as required by either mode
  * need a server side app for this
  * wrap all network streams into timeout operations (now only file download is done properly)