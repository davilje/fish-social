/**
 * Compatibility entry: all ecology resets now require the explicit safe L1 workflow.
 * Usage matches rebuild-fish-ecology.ts: absolute --db, --environment test,
 * --dry-run OR --apply with --backup-dir and --batch.
 * This file must never import db.ts or call resetAllEcology() on the default database.
 */
import './rebuild-fish-ecology.js';
