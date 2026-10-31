import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';
const root = '/Users/davilje/fish-social';
const output = `${root}/output/overlay-short-labels-2026-10-31`;
const wb = await SpreadsheetFile.importXlsx(await FileBlob.load(`${output}/labels-before.xlsx`));
const sheet = wb.worksheets.getItem('文本配置');
const rows = sheet.getUsedRange().values;
const header = rows.find(row => row[0] === 'Key');
const cn = header.indexOf('zhCN（可编辑）'), en = header.indexOf('enUS（可编辑）');
if (cn < 0 || en < 0) throw new Error('Missing editable language columns');
const labels = JSON.parse(await fs.readFile(`${output}/labels.json`, 'utf8'));
const addresses = JSON.parse(await fs.readFile(`${output}/label-rows.json`, 'utf8'));
const matches = Object.entries(addresses).map(([key, row]) => ({ key, row, after: labels[key] }));
if (matches.length !== Object.keys(labels).length) throw new Error('Missing label rows');
console.log(JSON.stringify(matches));
if (process.argv.includes('--edit')) {
  for (const { key, row, after } of matches) {
    if (sheet.getRange(`A${row}`).values[0][0] !== key) throw new Error(`Key mismatch ${row}`);
    sheet.getRange(`E${row}`).values = [[after[0]]];
    sheet.getRange(`F${row}`).values = [[after[1]]];
  }
  wb.recalculate();
  for (const { row, after } of matches) {
    if (sheet.getRange(`E${row}`).values[0][0] !== after[0] || sheet.getRange(`F${row}`).values[0][0] !== after[1]) throw new Error('Label mismatch');
  }
  const file = await SpreadsheetFile.exportXlsx(wb);
  await file.save(`${root}/unity-overlay文本表.xlsx`);
  const preview = await wb.render({ sheetName: '文本配置', range: 'D85:F103', format: 'png', scale: 1 });
  await fs.writeFile(`${output}/labels-after.png`, new Uint8Array(await preview.arrayBuffer()));
  console.log(`Saved ${matches.length} label rows`);
} else {
  const preview = await wb.render({ sheetName: '文本配置', range: 'D85:F103', format: 'png', scale: 1 });
  await fs.writeFile(`${output}/labels-before.png`, new Uint8Array(await preview.arrayBuffer()));
}
