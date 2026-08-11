import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const compact = fs.readFileSync(
  path.join(__dirname, '../docs/analytics/pond-day-simulation/compact.json'),
  'utf8',
);

const canvas = `import { useState, useMemo } from 'react';
import {
  Stack, Row, Grid, Card, CardBody, CardHeader,
  H1, Text, Stat, Table, Divider,
  LineChart, BarChart, PieChart, Callout, Select,
  useHostTheme,
} from 'cursor/canvas';

const QN = ['\u666e\u901a','\u4f18\u826f','\u7a00\u6709','\u53f2\u8bd7','\u4f20\u8bf4','\u795e\u8bdd','\u81f3\u5c0a'];
const BUCKETS = ['0.08-0.15','0.15-0.25','0.25-0.40','0.40-0.80','0.80-2.0','2.0-5.0','5.0+'];

const DATA = ${compact};

export default function PondDaySimulation() {
  const theme = useHostTheme();
  const [anglers, setAnglers] = useState(1);
  const [pondIdx, setPondIdx] = useState(0);

  const scenario = useMemo(
    () => DATA.scenarios.find((s) => s.a === anglers) ?? DATA.scenarios[1],
    [anglers],
  );
  const pond = scenario.ponds[pondIdx];

  const popSeries = [{ id: 'population', label: 'count', color: theme.accent }];
  const popData = pond.tl.map((t) => ({ x: t.h, y: t.n }));

  const sizeSeries = [{ id: 'avgSize', label: 'avg m', color: theme.text.secondary }];
  const sizeData = pond.tl.map((t) => ({ x: t.h, y: t.a }));

  const initBar = QN.map((name, i) => ({ x: name, y: pond.i.q[i] }));
  const finalBar = QN.map((name, i) => ({ x: name, y: pond.f.q[i].c }));

  const caughtPie = QN.map((name, i) => ({ name, value: pond.c.q[i].c })).filter(
    (d) => d.value > 0,
  );

  const anglerOpts = DATA.scenarios.map((s) => ({
    value: String(s.a),
    label: s.a + ' anglers/pond',
  }));
  const pondOpts = scenario.ponds.map((p, i) => ({ value: String(i), label: p.n }));

  return (
    <Stack gap={16}>
      <H1>Pond 24h Simulation v0.3.1</H1>
      <Text color={theme.text.secondary}>
        seed {DATA.seed} | 15min steps | 5min bite | no breeding
      </Text>

      <Row gap={12}>
        <Select
          label="Anglers"
          value={String(anglers)}
          options={anglerOpts}
          onChange={(v) => {
            setAnglers(Number(v));
            setPondIdx(0);
          }}
        />
        <Select
          label="Pond"
          value={String(pondIdx)}
          options={pondOpts}
          onChange={(v) => setPondIdx(Number(v))}
        />
      </Row>

      <Grid columns={4} gap={12}>
        <Stat label="Initial" value={String(pond.i.n)} hint={pond.i.avg + 'm avg'} />
        <Stat label="After 24h" value={String(pond.f.n)} hint={pond.f.avg + 'm avg'} />
        <Stat label="Caught" value={String(pond.c.n)} hint={pond.c.avg + 'm avg'} />
        <Stat label="Cap" value={String(pond.mx)} hint="maxPopulation" />
      </Grid>

      <Grid columns={2} gap={12}>
        <Card>
          <CardHeader title="Population over time" subtitle="hourly samples" />
          <CardBody>
            <LineChart
              data={popData}
              series={popSeries}
              xLabel="time"
              yLabel="fish count"
              height={220}
            />
          </CardBody>
        </Card>
        <Card>
          <CardHeader title="Avg size over time" subtitle="survivors weighted" />
          <CardBody>
            <LineChart
              data={sizeData}
              series={sizeSeries}
              xLabel="time"
              yLabel="size (m)"
              height={220}
            />
          </CardBody>
        </Card>
      </Grid>

      <Grid columns={2} gap={12}>
        <Card>
          <CardHeader title="Quality: initial vs 24h" />
          <CardBody>
            <BarChart
              data={initBar}
              series={[{ id: 'init', label: 'initial', color: theme.text.secondary }]}
              xLabel="quality"
              yLabel="count"
              height={200}
            />
            <Divider />
            <BarChart
              data={finalBar}
              series={[{ id: 'final', label: '24h left', color: theme.accent }]}
              xLabel="quality"
              yLabel="count"
              height={200}
            />
          </CardBody>
        </Card>
        <Card>
          <CardHeader
            title="Caught by quality"
            subtitle={pond.c.n + ' fish, avg ' + pond.c.avg + 'm'}
          />
          <CardBody>
            {caughtPie.length > 0 ? (
              <PieChart data={caughtPie} height={280} />
            ) : (
              <Callout tone="info">No catches in zero-angler scenario</Callout>
            )}
          </CardBody>
        </Card>
      </Grid>

      <Card>
        <CardHeader title="Size histogram (m)" />
        <CardBody>
          <Table
            columns={[
              { key: 'b', header: 'bucket' },
              { key: 'i', header: 'initial', align: 'right' },
              { key: 'f', header: '24h', align: 'right' },
            ]}
            rows={BUCKETS.map((b, i) => ({
              b: b + 'm',
              i: String(pond.i.h[i]),
              f: String(pond.f.h[i]),
            }))}
          />
        </CardBody>
      </Card>
    </Stack>
  );
}
`;

const out = path.join(
  process.env.USERPROFILE || '',
  '.cursor/projects/c-Users-Administrator-Projects-fish-social/canvases/pond-day-simulation.canvas.tsx',
);
fs.writeFileSync(out, canvas);
console.log('Wrote', out, canvas.length, 'bytes');
