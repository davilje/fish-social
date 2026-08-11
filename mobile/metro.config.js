const { getDefaultConfig } = require('expo/metro-config');
const path = require('path');

const projectRoot = __dirname;
const workspaceRoot = path.resolve(projectRoot, '..');
const sharedRoot = path.resolve(workspaceRoot, 'shared');

const config = getDefaultConfig(projectRoot);

// Only watch packages the app imports. Watching the whole monorepo makes Metro
// crawl data/fish-social.db (~5GB+) and crash with ERR_FS_FILE_TOO_LARGE (>2GiB).
config.watchFolders = [sharedRoot];
config.resolver.nodeModulesPaths = [
  path.resolve(projectRoot, 'node_modules'),
  path.resolve(workspaceRoot, 'node_modules'),
];

// Safety net (no regex flags — Metro cannot combine patterns with mixed flags)
const blockListPatterns = [
  /[\\/]data[\\/].*/,
  /[\\/]logs[\\/].*/,
  /[\\/]\.git[\\/].*/,
  /fish-social\.db$/,
  /fish-social\.db-wal$/,
  /fish-social\.db-shm$/,
];
const prevBlockList = config.resolver.blockList;
config.resolver.blockList = Array.isArray(prevBlockList)
  ? [...prevBlockList, ...blockListPatterns]
  : prevBlockList
    ? [prevBlockList, ...blockListPatterns]
    : blockListPatterns;

module.exports = config;
