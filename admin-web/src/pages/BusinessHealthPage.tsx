import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Chart,
  LineController,
  LineElement,
  PointElement,
  LinearScale,
  CategoryScale,
  Legend,
  Tooltip,
  Filler,
} from 'chart.js';
import { api, type BusinessHealthTrend } from '../api';
import { formatPondName } from '../pondNames';

Chart.register(
  LineController,
  LineElement,
  PointElement,
  LinearScale,
  CategoryScale,
  Legend,
  Tooltip,
  Filler,
);

export function BusinessHealthPage() {
  const [days, setDays] = useState(7);
  const [data, setData] = useState<BusinessHealthTrend | null>(null);
  const [error, setError] = useState('');
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const chartRef = useRef<Chart | null>(null);

  useEffect(() => {
    setError('');
    api
      .businessHealth(days)
      .then(setData)
      .catch((e: Error) => setError(e.message));
  }, [days]);

  useEffect(() => {
    if (!data || !canvasRef.current) return;
    const labels = data.daily.map((d) => d.dateKey);
    const catches = data.daily.map((d) => d.totalCatch);
    const discRates = data.daily.map((d) => +(d.disconnectRate * 100).toFixed(2));

    chartRef.current?.destroy();
    chartRef.current = new Chart(canvasRef.current, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: '日钓获',
            data: catches,
            borderColor: '#0e7490',
            backgroundColor: 'rgba(14,116,144,0.08)',
            tension: 0.25,
            yAxisID: 'y',
            fill: true,
          },
          {
            label: '断线率 %',
            data: discRates,
            borderColor: '#b91c1c',
            tension: 0.25,
            yAxisID: 'y1',
            borderDash: [4, 3],
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        scales: {
          y: { position: 'left', title: { display: true, text: '钓获' } },
          y1: {
            position: 'right',
            grid: { drawOnChartArea: false },
            title: { display: true, text: '断线率 %' },
            min: 0,
          },
        },
      },
    });

    return () => {
      chartRef.current?.destroy();
      chartRef.current = null;
    };
  }, [data]);

  const t = data?.totals;
  const avgPopSeries = useMemo(() => {
    if (!data) return [];
    return data.daily.map((d) => {
      const vals = d.ponds.map((p) => p.avgPopulation).filter((v): v is number => v != null);
      const avg = vals.length ? vals.reduce((a, b) => a + b, 0) / vals.length : null;
      return { dateKey: d.dateKey, avg };
    });
  }, [data]);

  return (
    <section>
      <h2>业务健康</h2>
      <p className="meta">汇总卡 + 折线趋势；日表明细默认折叠。</p>
      <div className="form-row">
        <label>
          天数
          <input
            type="number"
            min={1}
            max={30}
            value={days}
            onChange={(e) => setDays(Number(e.target.value) || 7)}
          />
        </label>
      </div>
      {error && <p className="err">{error}</p>}
      {data && t && (
        <>
          <div className="live-cards">
            <div className="live-card">
              <div className="lbl">总钓获{data.catchNote ? '（含机器人）' : ''}</div>
              <div className="val">{t.catchCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">总断线</div>
              <div className="val">{t.disconnectCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">上钩合计</div>
              <div className="val">{t.hookCount ?? 0}</div>
            </div>
            <div className="live-card">
              <div className="lbl">活跃玩家（日合计）</div>
              <div className="val">{t.activePlayers ?? 0}</div>
            </div>
          </div>

          <div className="chart-wrap">
            <canvas ref={canvasRef} />
          </div>

          {avgPopSeries.some((x) => x.avg != null) && (
            <p className="meta">
              分塘人口均值（参考）：
              {avgPopSeries
                .filter((x) => x.avg != null)
                .slice(-3)
                .map((x) => `${x.dateKey}=${x.avg!.toFixed(1)}`)
                .join(' · ')}
            </p>
          )}

          <details className="raw-fold">
            <summary>日表明细</summary>
            <table>
              <thead>
                <tr>
                  <th>日期</th>
                  <th>日钓获</th>
                  <th>断线率</th>
                  <th>分塘人口</th>
                </tr>
              </thead>
              <tbody>
                {data.daily.map((d) => (
                  <tr key={d.dateKey}>
                    <td>{d.dateKey}</td>
                    <td>{d.totalCatch}</td>
                    <td>{(d.disconnectRate * 100).toFixed(1)}%</td>
                    <td>
                      {d.ponds
                        .map((p) => `${formatPondName(p.pondId)}:${p.avgPopulation ?? '—'}`)
                        .join(', ') || '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </details>
        </>
      )}
    </section>
  );
}
