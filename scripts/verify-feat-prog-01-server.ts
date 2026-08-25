import '../server/src/db.js';
import { ensurePlayer } from '../server/src/players.js';
import {
  checkJoinPondAccess,
  completeOnboarding,
  ensurePlayerProgress,
  grantCatchProgress,
  resetOnboarding,
} from '../server/src/playerProgress.js';
import { joinPond, leavePond } from '../server/src/pondSession.js';
import { calcFishSellPrice } from '@fish-social/shared';

const id = 'test-prog-01-smoke';
leavePond('sock-prog-01');
ensurePlayer(id, 'Tester');
ensurePlayerProgress(id);
resetOnboarding(id);

const blocked = checkJoinPondAccess(id, 'pond-calm');
if (blocked.ok) throw new Error('expected calm blocked before onboarding');

const novice = checkJoinPondAccess(id, 'pond-novice');
if (!novice.ok) throw new Error(novice.error);

const joined = joinPond('sock-prog-01', 'pond-novice', 'Tester', id);
if (!joined.ok) throw new Error(joined.error);

grantCatchProgress(id, 'pond-novice', 'crucian', 'gray', 0.12);
completeOnboarding(id);

const after = ensurePlayerProgress(id);
if (!after.onboardingCompleted) throw new Error('onboarding not completed');

const noviceAgain = checkJoinPondAccess(id, 'pond-novice');
if (noviceAgain.ok) throw new Error('novice should be blocked after onboarding');

const calm = checkJoinPondAccess(id, 'pond-calm');
if (!calm.ok) throw new Error(calm.error);

const sell = calcFishSellPrice({ quality: 'blue', sizeM: 0.6, speciesId: 'carp' });
if (sell < 160) throw new Error(`sell too low: ${sell}`);

leavePond('sock-prog-01');
console.log('FEAT-PROG-01 server smoke ok, sell=', sell);
