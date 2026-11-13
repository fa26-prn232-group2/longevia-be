#!/usr/bin/env node
/* eslint-disable */
/**
 * build_uml.js
 * Sinh .drawio (XML khong nen) cho UML class diagram tu model JSON.
 *
 * Khac build_drawio.js (ban bang vat ly):
 *   - class box = header (xam) + attribute compartment
 *   - association = duong lien, multiplicity dat o 2 dau
 *   - package = khung mo + ten o goc tren trai
 *
 * Usage: node build_uml.js [model-name ...]
 */
'use strict';

const fs = require('fs');
const path = require('path');

const SRC_DIR = path.resolve(__dirname, '..');
const MODEL_DIR = path.join(SRC_DIR, 'model');
const OUT_DIR = path.join(SRC_DIR, 'drawio');

const FONT_SANS = "Segoe UI, Helvetica Neue, Arial, sans-serif";
const FONT_MONO = "Consolas, Courier New, monospace";

const HDR_BG = '#E2E8F0';
const BORDER = '#475569';
const INK = '#0F172A';

/* ------------------------------------------------------------------ */
function xesc(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

const CLASS_PAD = 8;
const HDR_H = 40;
const ROW_H = 28;

let M = { hdrH: HDR_H, rowH: ROW_H, pad: CLASS_PAD };

function classHeight(c) {
  const n = (c.attrs || []).length;
  return M.hdrH + n * M.rowH + M.pad;
}

function classValue(c) {
  const out = [];
  out.push(
    `<table style="width:100%;border-collapse:collapse;margin:0;padding:0;">` +
      `<tr><td style="background:${HDR_BG};border-bottom:1px solid ${BORDER};` +
      `font-family:${FONT_SANS};font-size:24px;font-weight:700;line-height:${M.hdrH - 8}px;` +
      `padding:4px 10px;color:${INK};white-space:nowrap;">${c.name}` +
      `${c.stereotype ? ` <span style="font-weight:400;font-size:22px;color:#475569">&laquo;${c.stereotype}&raquo;</span>` : ''}` +
      `</td></tr>`
  );
  for (const a of c.attrs || []) {
    const type = a.t ? `<span style="color:#475569"> : ${a.t}</span>` : '';
    const key = a.k
      ? ` <span style="color:#64748B;font-family:${FONT_SANS};font-size:22px">{${a.k}}</span>`
      : '';
    const under = a.k === 'PK' ? 'text-decoration:underline;' : '';
    out.push(
      `<tr><td style="font-family:${FONT_MONO};font-size:22px;line-height:${M.rowH}px;` +
        `padding:0 10px;color:#1E293B;white-space:nowrap;${under}">${a.n}${type}${key}</td></tr>`
    );
  }
  out.push(`</table>`);
  return out.join('');
}

function classStyle(c) {
  return (
    `rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=${BORDER};strokeWidth=2;` +
    `align=left;verticalAlign=top;spacing=0;spacingLeft=0;spacingTop=0;spacingRight=0;spacingBottom=0;` +
    `overflow=visible;`
  );
}

function pkgStyle(p) {
  return (
    `rounded=0;whiteSpace=wrap;html=1;fillColor=#F8FAFC;strokeColor=#94A3B8;strokeWidth=2;` +
    `dashed=0;verticalAlign=top;align=left;spacingLeft=16;spacingTop=10;` +
    `fontSize=26;fontStyle=1;fontColor=#334155;fontFamily=${FONT_SANS};`
  );
}

function assocStyle(a) {
  let s =
    `edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeWidth=2;strokeColor=${BORDER};` +
    `startArrow=none;endArrow=none;startFill=0;endFill=0;`;
  if (a.dashed) s += 'dashed=1;';
  if (a.ex !== undefined) s += `exitX=${a.ex};exitY=${a.ey};exitDx=0;exitDy=0;`;
  if (a.enx !== undefined) s += `entryX=${a.enx};entryY=${a.eny};entryDx=0;entryDy=0;`;
  return s;
}

const LBL_STYLE =
  `edgeLabel;html=1;align=center;verticalAlign=middle;resizable=0;points=[];` +
  `fontSize=22;fontFamily=${FONT_SANS};fontColor=#334155;labelBackgroundColor=#FFFFFF;`;

/* ------------------------------------------------------------------ */
function geoV(x, y, w, h) {
  return `<mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry" />`;
}

function geoE(a) {
  const pts = (a.points || []).map((p) => `<mxPoint x="${p[0]}" y="${p[1]}" />`).join('');
  const arr = pts ? `<Array as="points">${pts}</Array>` : '';
  return `<mxGeometry relative="1" as="geometry">${arr}</mxGeometry>`;
}

function build(model) {
  const W = (model.page && model.page.width) || 2560;
  const H = (model.page && model.page.height) || 1440;
  const G = (model.page && model.page.gridSize) || 8;

  const layers = [
    { id: 'layer-bg', name: 'Background' },
    { id: 'layer-packages', name: 'Packages' },
    { id: 'layer-classes', name: 'Classes' },
    { id: 'layer-assoc', name: 'Associations' },
    { id: 'layer-notes', name: 'Notes' },
  ];

  const cells = [];
  for (const l of layers) {
    cells.push(`        <mxCell id="${l.id}" value="${xesc(l.name)}" parent="0" />`);
  }

  // background (draw.io CLI crops to content -> need full page rect)
  const inset = 1;
  cells.push(
    `        <mxCell id="bg-page" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor=none;" vertex="1" parent="layer-bg">\n          ${geoV(
      inset,
      inset,
      W - inset * 2,
      H - inset * 2
    )}\n        </mxCell>`
  );

  for (const p of model.packages || []) {
    cells.push(
      `        <mxCell id="${p.id}" value="${xesc(p.name)}" style="${xesc(pkgStyle(p))}" vertex="1" parent="layer-packages">\n          ${geoV(
        p.x,
        p.y,
        p.w,
        p.h
      )}\n        </mxCell>`
    );
  }

  for (const c of model.classes || []) {
    const h = c.h || classHeight(c);
    cells.push(
      `        <mxCell id="${c.id}" value="${xesc(classValue(c))}" style="${xesc(classStyle(c))}" vertex="1" parent="layer-classes">\n          ${geoV(
        c.x,
        c.y,
        c.w,
        h
      )}\n        </mxCell>`
    );
  }

  for (const a of model.associations || []) {
    cells.push(
      `        <mxCell id="${a.id}" value="" style="${xesc(assocStyle(a))}" edge="1" parent="layer-assoc" ` +
        `source="${a.from}" target="${a.to}">\n          ${geoE(a)}\n        </mxCell>`
    );
    const labels = [];
    if (a.fromMult) labels.push({ v: a.fromMult, at: -0.72 });
    if (a.toMult) labels.push({ v: a.toMult, at: 0.72 });
    for (let i = 0; i < labels.length; i++) {
      const l = labels[i];
      cells.push(
        `        <mxCell id="${a.id}-l${i}" value="${xesc(l.v)}" style="${xesc(LBL_STYLE)}" ` +
          `vertex="1" connectable="0" parent="${a.id}">\n          ` +
          `<mxGeometry x="${l.at}" relative="1" as="geometry">\n            <mxPoint as="offset" x="0" y="0" />\n          </mxGeometry>\n        </mxCell>`
      );
    }
  }

  for (const t of model.texts || []) {
    const style =
      `text;html=1;whiteSpace=wrap;align=${t.align || 'left'};verticalAlign=middle;` +
      `fontSize=${t.fontSize || 22};fontColor=${t.fontColor || '#475569'};` +
      `fontStyle=${t.bold ? 1 : 0};fontFamily=${FONT_SANS};`;
    cells.push(
      `        <mxCell id="${t.id}" value="${xesc(t.text)}" style="${xesc(style)}" vertex="1" parent="layer-notes">\n          ${geoV(
        t.x,
        t.y,
        t.w,
        t.h || 40
      )}\n        </mxCell>`
    );
  }

  return (
    `<mxfile host="app.diagrams.net" pages="1">\n` +
    `  <diagram name="${xesc(model.name)}" id="${xesc(model.id)}">\n` +
    `    <mxGraphModel page="1" pageWidth="${W}" pageHeight="${H}" grid="1" gridSize="${G}" ` +
    `guides="1" tooltips="1" connect="1" arrows="1" fold="1" pageScale="1" pageFormat="landscape" ` +
    `math="0" shadow="0" border="0">\n` +
    `      <root>\n        <mxCell id="0" />\n        <mxCell id="1" parent="0" />\n` +
    cells.join('\n') +
    `\n      </root>\n    </mxGraphModel>\n  </diagram>\n</mxfile>\n`
  );
}

function main() {
  if (!fs.existsSync(OUT_DIR)) fs.mkdirSync(OUT_DIR, { recursive: true });
  const want = process.argv.slice(2);
  const files = fs
    .readdirSync(MODEL_DIR)
    .filter((f) => f.endsWith('.uml.json'))
    .filter((f) => (want.length ? want.some((w) => f.includes(w)) : true));

  if (!files.length) {
    console.error('Khong co model UML nao trong', MODEL_DIR);
    process.exit(1);
  }

  for (const f of files) {
    const model = JSON.parse(fs.readFileSync(path.join(MODEL_DIR, f), 'utf8'));\n    M = Object.assign({ hdrH: HDR_H, rowH: ROW_H, pad: CLASS_PAD }, model.metrics || {});
    const xml = build(model);
    const out = path.join(OUT_DIR, `${model.id}.drawio`);
    fs.writeFileSync(out, xml, 'utf8');
    console.log(
      `OK  ${f}  ->  ${path.relative(SRC_DIR, out)}  (${Buffer.byteLength(xml)} bytes)`
    );
  }
}

main();
