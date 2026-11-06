import json,csv
from decimal import Decimal
from pathlib import Path
root=Path(__file__).resolve().parents[1]
cells=json.loads(root.joinpath('Core/SizeCatalog.json').read_text(),parse_float=Decimal)
standard=[[Decimal(v) for v in row] for row in json.loads(root.joinpath('Tests/standard-width-fixture.json').read_text())]
with root.joinpath('Tests/data-width-notes.csv').open('w',encoding='utf-8-sig',newline='') as f:
 w=csv.writer(f);w.writerow(['要素','代号','IT等级','尺寸大于_mm','尺寸至_mm','上偏差_μm','下偏差_μm','实际宽度_μm','同级标准宽度_μm','处理'])
 for c in cells:
  if not c['widthWarning']:continue
  r=next(r for r in standard if r[0]<c['upperMm']<=r[1]);g=int(c['grade']);width=r[g+1]*(1000 if g>=12 else 1)
  w.writerow(['孔' if c['kind']=='hole' else '轴',c['zone'],g,c['lowerMm'],c['upperMm'],c['upperUm'],c['lowerUm'],c['upperUm']-c['lowerUm'],width,'保留实际偏差，查询时提示'])
