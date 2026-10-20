import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

type InventoryRow = {
  id: number; code: string; name: string; category: string; unit: string;
  physicalStock: number; reservedStock: number; availableStock: number;
  stockStatus: string; reorderLevel: number;
};
type Part = {
  id: number; code: string; name: string; category: string; specification: string;
  unit: string; unitCost: number; openingStock: number; reorderLevel: number;
  supplier: string; remarks: string;
};
type BomLine = { id: number; partId: number; quantityPerProduct: number; part?: Part };
type Product = { id: number; code: string; name: string; description: string; sellingPrice: number; bomLines: BomLine[] };

const emptyPart = (): Partial<Part> => ({ code: '', name: '', category: '', specification: '', unit: 'pc', unitCost: 0, openingStock: 0, reorderLevel: 5, supplier: '', remarks: '' });
const emptyProduct = (): Partial<Product> => ({ code: '', name: '', description: '', sellingPrice: 0 });

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="shell">
    <aside class="sidebar">
      <div class="brand"><div class="mark">F</div><div><b>FOGERITHM</b><small>ADMIN CONSOLE</small></div></div>
      <div class="nav-label">WORKSPACE</div>
      <button type="button" class="nav" [class.active]="section==='overview'" (click)="go('overview')">▦ &nbsp; Overview</button>
      <button type="button" class="nav" [class.active]="section==='inventory'" (click)="go('inventory')">▤ &nbsp; Parts & Inventory</button>
      <button type="button" class="nav" [class.active]="section==='products'" (click)="go('products')">⬡ &nbsp; Products & BOM</button>
      <button type="button" class="nav" [class.active]="section==='orders'" (click)="go('orders')">▣ &nbsp; Customer Orders</button>
      <button type="button" class="nav" [class.active]="section==='production'" (click)="go('production')">⚙ &nbsp; Production</button>
      <button type="button" class="nav" [class.active]="section==='purchases'" (click)="go('purchases')">↙ &nbsp; Purchases</button>
      <div class="side-bottom"><span class="dot"></span> Local database<br><small>Stored on this device</small></div>
    </aside>
    <main class="main">
      <header><div><span class="eyebrow">FOGERITHM / ADMIN</span><h1>{{ pageTitle }}</h1><p>Inventory and manufacturing workspace</p></div><div class="user-pill"><span class="avatar">S</span><span><b>Administrator</b><small>Local workspace</small></span></div></header>
      <div *ngIf="notice" class="notice">{{notice}}</div><div *ngIf="error" class="error">{{error}}</div>

      <section class="metrics" *ngIf="section==='overview' || section==='inventory'">
        <article class="metric"><span class="metric-label">Tracked components</span><strong>{{ inventory.length }}</strong><small>Parts in master list</small></article>
        <article class="metric"><span class="metric-label">Low / out of stock</span><strong class="warning">{{ attentionCount }}</strong><small>Review before accepting orders</small></article>
        <article class="metric"><span class="metric-label">Total physical units</span><strong>{{ totalPhysical | number:'1.0-2' }}</strong><small>Across tracked components</small></article>
        <article class="metric"><span class="metric-label">Reserved units</span><strong>{{ totalReserved | number:'1.0-2' }}</strong><small>Accepted-order requirements</small></article>
      </section>

      <ng-container *ngIf="section==='overview'">
        <section class="panel"><div class="panel-head"><div><h2>Inventory overview</h2><p>Physical stock, reservations and available quantities are tracked separately.</p></div><button class="primary" (click)="go('inventory')">+ Add first part</button></div>
          <div *ngIf="inventory.length===0" class="empty"><b>No parts entered yet</b><br>Start by adding your raw materials and components manually.</div>
          <div class="table-wrap" *ngIf="inventory.length"><table><thead><tr><th>PART</th><th>CATEGORY</th><th>PHYSICAL</th><th>RESERVED</th><th>AVAILABLE</th><th>STATUS</th></tr></thead><tbody><tr *ngFor="let row of inventory"><td><b>{{row.name}}</b><small>{{row.code}}</small></td><td>{{row.category || '—'}}</td><td>{{row.physicalStock}} {{row.unit}}</td><td>{{row.reservedStock}} {{row.unit}}</td><td><b>{{row.availableStock}} {{row.unit}}</b></td><td><span class="status" [class.low]="row.stockStatus==='LOW STOCK'" [class.out]="row.stockStatus==='OUT OF STOCK'">{{row.stockStatus}}</span></td></tr></tbody></table></div>
        </section>
      </ng-container>

      <ng-container *ngIf="section==='inventory'">
        <section class="panel"><div class="panel-head"><div><h2>Add a part / raw material</h2><p>Enter one component at a time. Opening stock is your current physical stock at setup.</p></div></div>
          <form class="form-grid" (ngSubmit)="savePart()">
            <label>Part code *<input name="partCode" [(ngModel)]="partForm.code" required maxlength="50" placeholder="e.g. PUMP-110" /></label>
            <label>Part name *<input name="partName" [(ngModel)]="partForm.name" required maxlength="150" placeholder="e.g. 110 PSI pump" /></label>
            <label>Category<input name="partCategory" [(ngModel)]="partForm.category" placeholder="Pump / Battery / Pipe" /></label>
            <label>Unit<select name="partUnit" [(ngModel)]="partForm.unit"><option value="pc">pc</option><option value="set">set</option><option value="m">m</option><option value="kg">kg</option><option value="g">g</option><option value="L">L</option></select></label>
            <label>Specification<input name="partSpec" [(ngModel)]="partForm.specification" placeholder="Voltage, size, rating, etc." /></label>
            <label>Unit cost (₹)<input name="partCost" type="number" min="0" step="0.01" [(ngModel)]="partForm.unitCost" /></label>
            <label>Opening physical stock<input name="partStock" type="number" min="0" step="0.0001" [(ngModel)]="partForm.openingStock" /></label>
            <label>Reorder level<input name="partReorder" type="number" min="0" step="0.0001" [(ngModel)]="partForm.reorderLevel" /></label>
            <label>Supplier<input name="partSupplier" [(ngModel)]="partForm.supplier" placeholder="Supplier name" /></label>
            <label class="wide">Remarks<input name="partRemarks" [(ngModel)]="partForm.remarks" placeholder="Optional notes" /></label>
            <div class="form-actions"><button class="primary" type="submit" [disabled]="saving">{{saving ? 'Saving…' : 'Save part'}}</button><button class="secondary" type="button" (click)="resetPart()">Clear form</button></div>
          </form>
        </section>
        <section class="panel section-gap"><div class="panel-head"><div><h2>Parts inventory</h2><p>Available stock = physical stock − active reservations.</p></div><button class="secondary" (click)="refreshAll()">↻ Refresh</button></div>
          <div class="table-wrap" *ngIf="inventory.length"><table><thead><tr><th>CODE / PART</th><th>CATEGORY</th><th>PHYSICAL</th><th>RESERVED</th><th>AVAILABLE</th><th>REORDER LEVEL</th><th>STATUS</th></tr></thead><tbody><tr *ngFor="let row of inventory"><td><b>{{row.code}}</b><small>{{row.name}}</small></td><td>{{row.category || '—'}}</td><td>{{row.physicalStock}} {{row.unit}}</td><td>{{row.reservedStock}} {{row.unit}}</td><td><b>{{row.availableStock}} {{row.unit}}</b></td><td>{{row.reorderLevel}}</td><td><span class="status" [class.low]="row.stockStatus==='LOW STOCK'" [class.out]="row.stockStatus==='OUT OF STOCK'">{{row.stockStatus}}</span></td></tr></tbody></table></div>
          <div *ngIf="!inventory.length" class="empty">No parts saved yet.</div>
        </section>
      </ng-container>

      <ng-container *ngIf="section==='products'">
        <section class="panel"><div class="panel-head"><div><h2>Create a product</h2><p>Add the finished product first, then define its bill of materials below.</p></div></div>
          <form class="form-grid" (ngSubmit)="saveProduct()">
            <label>Product code *<input name="productCode" [(ngModel)]="productForm.code" required placeholder="e.g. FG-3200" /></label>
            <label>Product name *<input name="productName" [(ngModel)]="productForm.name" required placeholder="e.g. Fogerithm Vector 3200" /></label>
            <label>Selling price (₹)<input name="productPrice" type="number" min="0" step="0.01" [(ngModel)]="productForm.sellingPrice" /></label>
            <label>Description<input name="productDescription" [(ngModel)]="productForm.description" placeholder="Short product description" /></label>
            <div class="form-actions"><button class="primary" type="submit" [disabled]="saving">{{saving ? 'Saving…' : 'Save product'}}</button><button class="secondary" type="button" (click)="resetProduct()">Clear form</button></div>
          </form>
        </section>
        <section class="panel section-gap"><div class="panel-head"><div><h2>Product BOM builder</h2><p>Add one component per BOM line. Quantity means the amount needed to make one finished product.</p></div></div>
          <div class="form-grid">
            <label class="wide">Choose product<select name="bomProduct" [(ngModel)]="selectedProductId" (ngModelChange)="onSelectedProductChange()"><option [ngValue]="null">Select a product…</option><option *ngFor="let product of products" [ngValue]="product.id">{{product.code}} — {{product.name}}</option></select></label>
            <label>Component / part<select name="bomPart" [(ngModel)]="bomPartId"><option [ngValue]="null">Select a part…</option><option *ngFor="let part of parts" [ngValue]="part.id">{{part.code}} — {{part.name}} ({{part.unit}})</option></select></label>
            <label>Qty per product<input name="bomQty" type="number" min="0.0001" step="0.0001" [(ngModel)]="bomQuantity" /></label>
            <div class="form-actions"><button class="primary" type="button" (click)="addBomLine()" [disabled]="!selectedProductId || !bomPartId || !bomQuantity || saving">+ Add BOM line</button></div>
          </div>
          <div class="table-wrap" *ngIf="selectedProduct"><table><thead><tr><th>PART CODE</th><th>COMPONENT</th><th>QTY / PRODUCT</th><th>UNIT</th><th></th></tr></thead><tbody><tr *ngFor="let line of selectedProduct.bomLines"><td><b>{{line.part?.code || partCode(line.partId)}}</b></td><td>{{line.part?.name || partName(line.partId)}}</td><td>{{line.quantityPerProduct}}</td><td>{{line.part?.unit || partUnit(line.partId)}}</td><td><button class="danger" type="button" (click)="removeBomLine(line.id)">Remove</button></td></tr></tbody></table></div>
          <div *ngIf="selectedProduct && !selectedProduct.bomLines.length" class="empty">No BOM lines yet. Add the first required component above.</div>
          <div *ngIf="!products.length" class="empty">Create a product first. Parts must also be entered before you can build a BOM.</div>
        </section>
        <section class="panel section-gap"><div class="panel-head"><div><h2>Product master</h2><p>{{products.length}} product(s) saved</p></div><button class="secondary" (click)="loadProducts()">↻ Refresh</button></div>
          <div class="table-wrap" *ngIf="products.length"><table><thead><tr><th>CODE</th><th>PRODUCT</th><th>SELLING PRICE</th><th>BOM LINES</th></tr></thead><tbody><tr *ngFor="let product of products"><td><b>{{product.code}}</b></td><td>{{product.name}}<small>{{product.description}}</small></td><td>₹{{product.sellingPrice | number:'1.2-2'}}</td><td>{{product.bomLines.length}}</td></tr></tbody></table></div>
        </section>
      </ng-container>

      <section class="panel" *ngIf="section==='orders'"><div class="panel-head"><div><h2>Customer Orders</h2><p>Next: enter orders manually and reserve BOM materials when accepted.</p></div></div><div class="empty">First complete the parts master and product BOM. This protects stock calculations from incomplete component data.</div></section>
      <section class="panel" *ngIf="section==='production'"><div class="panel-head"><div><h2>Production</h2><p>Next: record manufacturing and actual material consumption.</p></div></div><div class="empty">Production entry will deduct physical stock only when material is actually consumed.</div></section>
      <section class="panel" *ngIf="section==='purchases'"><div class="panel-head"><div><h2>Purchases &amp; Suppliers</h2><p>Next: enter supplier purchases and receive stock.</p></div></div><div class="empty">Purchase receiving will increase physical stock through stock movements.</div></section>
      <footer>FOGERITHM ADMIN <span>Manual entry · Local database · Stock integrity by design</span></footer>
    </main>
  </div>`
})
export class AppComponent implements OnInit {
  private http = inject(HttpClient);
  private readonly api = 'http://localhost:5080/api';
  section = 'overview';
  inventory: InventoryRow[] = [];
  parts: Part[] = [];
  products: Product[] = [];
  partForm: Partial<Part> = emptyPart();
  productForm: Partial<Product> = emptyProduct();
  selectedProductId: number | null = null;
  bomPartId: number | null = null;
  bomQuantity = 1;
  saving = false;
  loading = false;
  error = '';
  notice = '';
  get pageTitle() { const titles: Record<string,string> = {overview:'Overview',inventory:'Parts & Inventory',products:'Products & BOM',orders:'Customer Orders',production:'Production',purchases:'Purchases'}; return titles[this.section] || 'Overview'; }
  get attentionCount() { return this.inventory.filter(x => x.stockStatus !== 'OK').length; }
  get totalPhysical() { return this.inventory.reduce((a,x)=>a+Number(x.physicalStock||0),0); }
  get totalReserved() { return this.inventory.reduce((a,x)=>a+Number(x.reservedStock||0),0); }
  get selectedProduct() { return this.products.find(p => p.id === Number(this.selectedProductId)); }
  ngOnInit() { this.refreshAll(); }
  go(section: string) { this.section = section; this.error = ''; this.notice = ''; if (section === 'products') { this.loadParts(); this.loadProducts(); } if (section === 'inventory') { this.loadParts(); this.loadInventory(); } }
  refreshAll() { this.loadInventory(); this.loadParts(); this.loadProducts(); }
  loadInventory() { this.http.get<InventoryRow[]>(`${this.api}/inventory`).subscribe({next: rows => this.inventory = rows, error: e => this.showError(e, 'Could not load inventory. Check that the backend is running.')}); }
  loadParts() { this.http.get<Part[]>(`${this.api}/parts`).subscribe({next: rows => this.parts = rows, error: e => this.showError(e, 'Could not load parts.')}); }
  loadProducts() { this.http.get<Product[]>(`${this.api}/products`).subscribe({next: rows => { this.products = rows.map(p => ({...p, bomLines: p.bomLines || []})); if (this.selectedProductId && !this.products.some(p => p.id === this.selectedProductId)) this.selectedProductId = null; }, error: e => this.showError(e, 'Could not load products.')}); }
  savePart() {
    if (!this.partForm.code?.trim() || !this.partForm.name?.trim()) { this.error = 'Part code and name are required.'; return; }
    this.saving = true; this.clearMessages();
    this.http.post<Part>(`${this.api}/parts`, this.partForm).subscribe({next: saved => { this.saving = false; this.notice = `Part ${saved.code} saved successfully.`; this.partForm = emptyPart(); this.refreshAll(); }, error: e => { this.saving = false; this.showError(e, 'Could not save the part.'); }});
  }
  resetPart() { this.partForm = emptyPart(); this.clearMessages(); }
  saveProduct() {
    if (!this.productForm.code?.trim() || !this.productForm.name?.trim()) { this.error = 'Product code and name are required.'; return; }
    this.saving = true; this.clearMessages();
    this.http.post<Product>(`${this.api}/products`, this.productForm).subscribe({next: saved => { this.saving = false; this.notice = `Product ${saved.code} saved. Now add its BOM lines.`; this.productForm = emptyProduct(); this.refreshAll(); this.selectedProductId = saved.id; }, error: e => { this.saving = false; this.showError(e, 'Could not save the product.'); }});
  }
  resetProduct() { this.productForm = emptyProduct(); this.clearMessages(); }
  onSelectedProductChange() { this.bomPartId = null; this.bomQuantity = 1; }
  addBomLine() {
    if (!this.selectedProductId || !this.bomPartId || !Number(this.bomQuantity) || this.bomQuantity <= 0) { this.error = 'Choose a product, a component, and a quantity greater than zero.'; return; }
    this.saving = true; this.clearMessages();
    this.http.post<BomLine>(`${this.api}/products/${this.selectedProductId}/bom-lines`, {partId: this.bomPartId, quantityPerProduct: this.bomQuantity}).subscribe({next: () => { this.saving = false; this.notice = 'BOM line added.'; this.bomPartId = null; this.bomQuantity = 1; this.loadProducts(); }, error: e => { this.saving = false; this.showError(e, 'Could not add BOM line.'); }});
  }
  removeBomLine(lineId: number) {
    if (!confirm('Remove this component from the product BOM?')) return;
    this.http.delete(`${this.api}/bom-lines/${lineId}`).subscribe({next: () => { this.notice = 'BOM line removed.'; this.loadProducts(); }, error: e => this.showError(e, 'Could not remove BOM line.')});
  }
  partCode(id: number) { return this.parts.find(p => p.id === id)?.code || `PART-${id}`; }
  partName(id: number) { return this.parts.find(p => p.id === id)?.name || 'Unknown part'; }
  partUnit(id: number) { return this.parts.find(p => p.id === id)?.unit || ''; }
  private clearMessages() { this.error = ''; this.notice = ''; }
  private showError(e: any, fallback: string) { const message = e?.error?.message || e?.error?.title || fallback; this.error = message; }
}
