
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

type InventoryRow = {
  id: number;
  code: string;
  name: string;
  category: string;
  unit: string;
  physicalStock: number;
  reservedStock: number;
  availableStock: number;
  stockStatus: string;
  reorderLevel: number;
};

type Part = {
  id: number;
  code: string;
  name: string;
  category: string;
  specification: string;
  unit: string;
  unitCost: number;
  openingStock: number;
  reorderLevel: number;
  supplier: string;
  remarks: string;
};

type BomLine = {
  id: number;
  partId: number;
  quantityPerProduct: number;
  part?: Part;
};

type Product = {
  id: number;
  code: string;
  name: string;
  description: string;
  sellingPrice: number;
  bomLines: BomLine[];
};

type OrderLine = { id: number; orderId: number; productId: number; productCode: string; productName: string; quantity: number };
type CustomerOrder = { id: number; orderNumber: string; customerName: string; createdAt: string; status: string; lines: OrderLine[] };
type ProductionConsumption = { id?: number; partId: number; partName?: string; actualPartId?: number | null; actualPartName?: string | null; plannedQuantity: number; actualQuantity: number };
type ProductionRun = { id: number; productionNumber: string; orderId: number; orderNumber?: string; productId: number; productName?: string; plannedQuantity: number; completedQuantity: number; status: string; startedAt: string; completedAt?: string | null; consumptions?: ProductionConsumption[] };
type FinishedGood = { id: number; code: string; name: string; sellingPrice: number; physicalStock: number; deliveryQuantity?: number; deliveryReference?: string; deliveryNotes?: string };

const emptyPart = (): Partial<Part> => ({
  code: '',
  name: '',
  category: '',
  specification: '',
  unit: 'pc',
  unitCost: 0,
  openingStock: 0,
  reorderLevel: 5,
  supplier: '',
  remarks: ''
});

const emptyProduct = (): Partial<Product> => ({
  code: '',
  name: '',
  description: '',
  sellingPrice: 0
});

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">
        <div class="mark">F</div>
        <div>
          <b>FOGERITHM</b>
          <small>ADMIN CONSOLE</small>
        </div>
      </div>

      <div class="nav-label">WORKSPACE</div>

      <button type="button" class="nav"
        [class.active]="section==='overview'"
        (click)="go('overview')">▦ &nbsp; Overview</button>

      <button type="button" class="nav"
        [class.active]="section==='inventory'"
        (click)="go('inventory')">▤ &nbsp; Parts &amp; Inventory</button>

      <button type="button" class="nav"
        [class.active]="section==='products'"
        (click)="go('products')">⬡ &nbsp; Products &amp; BOM</button>

      <button type="button" class="nav"
        [class.active]="section==='orders'"
        (click)="go('orders')">▣ &nbsp; Customer Orders</button>

      <button type="button" class="nav"
        [class.active]="section==='production'"
        (click)="go('production')">⚙ &nbsp; Production</button>

      <button type="button" class="nav"
        [class.active]="section==='purchases'"
        (click)="go('purchases')">↙ &nbsp; Purchases</button>

      <div class="side-bottom">
        <span class="dot"></span> Local database<br>
        <small>Stored on this device</small>
      </div>
    </aside>

    <main class="main">
      <header>
        <div>
          <span class="eyebrow">FOGERITHM / ADMIN</span>
          <h1>{{ pageTitle }}</h1>
          <p>Inventory and manufacturing workspace</p>
        </div>

        <div class="user-pill">
          <span class="avatar">S</span>
          <span>
            <b>Administrator</b>
            <small>Local workspace</small>
          </span>
        </div>
      </header>

      <div *ngIf="notice" class="notice">{{ notice }}</div>
      <div *ngIf="error" class="error">{{ error }}</div>

      <!-- OVERVIEW METRICS -->
      <section class="metrics"
        *ngIf="section==='overview' || section==='inventory'">

        <article class="metric">
          <span class="metric-label">Tracked components</span>
          <strong>{{ inventory.length }}</strong>
          <small>Parts in master list</small>
        </article>

        <article class="metric">
          <span class="metric-label">Low / out of stock</span>
          <strong class="warning">{{ attentionCount }}</strong>
          <small>Review before accepting orders</small>
        </article>

        <article class="metric">
          <span class="metric-label">Total physical units</span>
          <strong>{{ totalPhysical | number:'1.0-2' }}</strong>
          <small>Across tracked components</small>
        </article>

        <article class="metric">
          <span class="metric-label">Reserved units</span>
          <strong>{{ totalReserved | number:'1.0-2' }}</strong>
          <small>Accepted-order requirements</small>
        </article>
      </section>

      <!-- OVERVIEW -->
      <ng-container *ngIf="section==='overview'">
        <section class="panel">
          <div class="panel-head">
            <div>
              <h2>Inventory overview</h2>
              <p>Physical stock, reservations and available quantities are tracked separately.</p>
            </div>
            <button type="button" class="primary"
              (click)="go('inventory')">+ Add first part</button>
          </div>

          <div *ngIf="inventory.length===0" class="empty">
            <b>No parts entered yet</b><br>
            Start by adding your raw materials and components manually.
          </div>

          <div class="table-wrap" *ngIf="inventory.length">
            <table>
              <thead>
                <tr>
                  <th>PART</th>
                  <th>CATEGORY</th>
                  <th>PHYSICAL</th>
                  <th>RESERVED</th>
                  <th>AVAILABLE</th>
                  <th>STATUS</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of inventory">
                  <td>
                    <b>{{ row.name }}</b>
                    <small>{{ row.code }}</small>
                  </td>
                  <td>{{ row.category || '—' }}</td>
                  <td>{{ row.physicalStock }} {{ row.unit }}</td>
                  <td>{{ row.reservedStock }} {{ row.unit }}</td>
                  <td><b>{{ row.availableStock }} {{ row.unit }}</b></td>
                  <td>
                    <span class="status"
                      [class.low]="row.stockStatus==='LOW STOCK'"
                      [class.out]="row.stockStatus==='OUT OF STOCK'">
                      {{ row.stockStatus }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </ng-container>

      <!-- PARTS AND INVENTORY -->
      <ng-container *ngIf="section==='inventory'">

        <section class="panel">
          <div class="panel-head">
            <div>
              <h2>{{ editingPartId ? 'Edit part / raw material' : 'Add a part / raw material' }}</h2>
              <p *ngIf="!editingPartId">
                Enter one component at a time. Opening stock is your physical stock at setup.
              </p>
              <p *ngIf="editingPartId">
                Editing part details will not change its opening stock or stock history.
              </p>
            </div>
          </div>

          <form class="form-grid" (ngSubmit)="savePart()">

            <label>
              Part code *
              <input name="partCode"
                [(ngModel)]="partForm.code"
                required maxlength="50"
                placeholder="e.g. PUMP-110">
            </label>

            <label>
              Part name *
              <input name="partName"
                [(ngModel)]="partForm.name"
                required maxlength="150"
                placeholder="e.g. 110 PSI pump">
            </label>

            <label>
              Category
              <input name="partCategory"
                [(ngModel)]="partForm.category"
                placeholder="Pump / Battery / Pipe">
            </label>

            <label>
              Unit
              <select name="partUnit" [(ngModel)]="partForm.unit">
                <option value="pc">pc</option>
                <option value="set">set</option>
                <option value="m">m</option>
                <option value="kg">kg</option>
                <option value="g">g</option>
                <option value="L">L</option>
              </select>
            </label>

            <label>
              Specification
              <input name="partSpec"
                [(ngModel)]="partForm.specification"
                placeholder="Voltage, size, rating, etc.">
            </label>

            <label>
              Unit cost (₹)
              <input name="partCost" type="number"
                min="0" step="0.01"
                [(ngModel)]="partForm.unitCost">
            </label>

            <label *ngIf="!editingPartId">
              Opening physical stock
              <input name="partStock" type="number"
                min="0" step="0.0001"
                [(ngModel)]="partForm.openingStock">
            </label>

            <label>
              Reorder level
              <input name="partReorder" type="number"
                min="0" step="0.0001"
                [(ngModel)]="partForm.reorderLevel">
            </label>

            <label>
              Supplier
              <input name="partSupplier"
                [(ngModel)]="partForm.supplier"
                placeholder="Supplier name">
            </label>

            <label class="wide">
              Remarks
              <input name="partRemarks"
                [(ngModel)]="partForm.remarks"
                placeholder="Optional notes">
            </label>

            <div class="form-actions">
              <button class="primary" type="submit" [disabled]="saving">
                {{ saving ? 'Saving…' : (editingPartId ? 'Update part' : 'Save part') }}
              </button>

              <button class="secondary" type="button"
                (click)="resetPart()">Cancel / Clear</button>
            </div>
          </form>
        </section>

        <!-- INVENTORY TABLE -->
        <section class="panel section-gap">
          <div class="panel-head">
            <div>
              <h2>Parts inventory</h2>
              <p>Available stock = physical stock − active reservations.</p>
            </div>
            <button type="button" class="secondary"
              (click)="refreshAll()">↻ Refresh</button>
          </div>

          <div class="table-wrap" *ngIf="inventory.length">
            <table>
              <thead>
                <tr>
                  <th>CODE / PART</th>
                  <th>CATEGORY</th>
                  <th>PHYSICAL</th>
                  <th>RESERVED</th>
                  <th>AVAILABLE</th>
                  <th>REORDER LEVEL</th>
                  <th>STATUS</th>
                  <th>ACTIONS</th>
                </tr>
              </thead>

              <tbody>
                <tr *ngFor="let row of inventory">
                  <td>
                    <b>{{ row.code }}</b>
                    <small>{{ row.name }}</small>
                  </td>

                  <td>{{ row.category || '—' }}</td>
                  <td>{{ row.physicalStock }} {{ row.unit }}</td>
                  <td>{{ row.reservedStock }} {{ row.unit }}</td>
                  <td><b>{{ row.availableStock }} {{ row.unit }}</b></td>
                  <td>{{ row.reorderLevel }}</td>

                  <td>
                    <span class="status"
                      [class.low]="row.stockStatus==='LOW STOCK'"
                      [class.out]="row.stockStatus==='OUT OF STOCK'">
                      {{ row.stockStatus }}
                    </span>
                  </td>

                  <td>
                    <div class="actions">
                      <button type="button" class="secondary"
                        [disabled]="saving"
                        (click)="editPart(row.id)">Edit</button>

                      <button type="button" class="danger"
                        [disabled]="saving"
                        (click)="deletePart(row.id)">Delete</button>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div *ngIf="!inventory.length" class="empty">
            No parts saved yet.
          </div>
        </section>

        <!-- STOCK ADJUSTMENT -->
        <section class="panel section-gap">
          <div class="panel-head">
            <div>
              <h2>Stock adjustment</h2>
              <p>
                Use positive quantities to add stock and negative quantities
                to remove stock. Every adjustment requires a reason.
              </p>
            </div>
          </div>

          <form class="form-grid" (ngSubmit)="adjustStock()">

            <label class="wide">
              Component / part *
              <select name="adjustPart"
                [(ngModel)]="adjustmentForm.partId" required>
                <option [ngValue]="null">Select a part…</option>
                <option *ngFor="let part of parts" [ngValue]="part.id">
                  {{ part.code }} — {{ part.name }} ({{ part.unit }})
                </option>
              </select>
            </label>

            <label>
              Quantity change *
              <input name="quantityChange" type="number"
                step="0.0001"
                [(ngModel)]="adjustmentForm.quantityChange" required>
            </label>

            <label>
              Reason *
              <input name="adjustmentReason"
                [(ngModel)]="adjustmentForm.reason"
                placeholder="Damaged, missing, count correction…"
                required>
            </label>

            <div class="form-actions">
              <button class="primary" type="submit" [disabled]="saving">
                {{ saving ? 'Saving…' : 'Record adjustment' }}
              </button>
            </div>
          </form>
        </section>
      </ng-container>

      <!-- PRODUCTS AND BOM -->
      <ng-container *ngIf="section==='products'">

        <section class="panel">
          <div class="panel-head">
            <div>
              <h2>Create a product</h2>
              <p>Add the finished product first, then define its bill of materials.</p>
            </div>
          </div>

          <form class="form-grid" (ngSubmit)="saveProduct()">
            <label>
              Product code *
              <input name="productCode"
                [(ngModel)]="productForm.code"
                required placeholder="e.g. FG-3200">
            </label>

            <label>
              Product name *
              <input name="productName"
                [(ngModel)]="productForm.name"
                required placeholder="e.g. Fogerithm Vector 3200">
            </label>

            <label>
              Selling price (₹)
              <input name="productPrice" type="number"
                min="0" step="0.01"
                [(ngModel)]="productForm.sellingPrice">
            </label>

            <label>
              Description
              <input name="productDescription"
                [(ngModel)]="productForm.description"
                placeholder="Short product description">
            </label>

            <div class="form-actions">
              <button class="primary" type="submit" [disabled]="saving">
                {{ saving ? 'Saving…' : 'Save product' }}
              </button>
              <button class="secondary" type="button"
                (click)="resetProduct()">Clear form</button>
            </div>
          </form>
        </section>

        <section class="panel section-gap">
          <div class="panel-head">
            <div>
              <h2>Product BOM builder</h2>
              <p>Quantity means the amount of a component needed for one finished product.</p>
            </div>
          </div>

          <div class="form-grid">
            <label class="wide">
              Choose product
              <select name="bomProduct"
                [(ngModel)]="selectedProductId"
                (ngModelChange)="onSelectedProductChange()">
                <option [ngValue]="null">Select a product…</option>
                <option *ngFor="let product of products"
                  [ngValue]="product.id">
                  {{ product.code }} — {{ product.name }}
                </option>
              </select>
            </label>

            <label>
              Component / part
              <select name="bomPart" [(ngModel)]="bomPartId">
                <option [ngValue]="null">Select a part…</option>
                <option *ngFor="let part of parts" [ngValue]="part.id">
                  {{ part.code }} — {{ part.name }} ({{ part.unit }})
                </option>
              </select>
            </label>

            <label>
              Qty per product
              <input name="bomQty" type="number"
                min="0.0001" step="0.0001"
                [(ngModel)]="bomQuantity">
            </label>

            <div class="form-actions">
              <button class="primary" type="button"
                (click)="addBomLine()"
                [disabled]="!selectedProductId || !bomPartId || !bomQuantity || saving">
                + Add BOM line
              </button>
            </div>
          </div>

          <div class="table-wrap" *ngIf="selectedProduct">
            <table>
              <thead>
                <tr>
                  <th>PART CODE</th>
                  <th>COMPONENT</th>
                  <th>QTY / PRODUCT</th>
                  <th>UNIT</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let line of selectedProduct.bomLines">
                  <td><b>{{ line.part?.code || partCode(line.partId) }}</b></td>
                  <td>{{ line.part?.name || partName(line.partId) }}</td>
                  <td>{{ line.quantityPerProduct }}</td>
                  <td>{{ line.part?.unit || partUnit(line.partId) }}</td>
                  <td>
                    <button type="button" class="danger"
                      (click)="removeBomLine(line.id)">Remove</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div *ngIf="selectedProduct && !selectedProduct.bomLines.length"
            class="empty">
            No BOM lines yet. Add the first required component above.
          </div>

          <div *ngIf="!products.length" class="empty">
            Create a product first. Parts must also be entered before you can build a BOM.
          </div>
        </section>

        <section class="panel section-gap">
          <div class="panel-head">
            <div>
              <h2>Product master</h2>
              <p>{{ products.length }} product(s) saved</p>
            </div>
            <button type="button" class="secondary"
              (click)="loadProducts()">↻ Refresh</button>
          </div>

          <div class="table-wrap" *ngIf="products.length">
            <table>
              <thead>
                <tr>
                  <th>CODE</th>
                  <th>PRODUCT</th>
                  <th>SELLING PRICE</th>
                  <th>BOM LINES</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let product of products">
                  <td><b>{{ product.code }}</b></td>
                  <td>
                    {{ product.name }}
                    <small>{{ product.description }}</small>
                  </td>
                  <td>₹{{ product.sellingPrice | number:'1.2-2' }}</td>
                  <td>{{ product.bomLines.length }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </ng-container>

      <!-- CUSTOMER ORDERS -->
      <ng-container *ngIf="section==='orders'">
        <section class="panel">
          <div class="panel-head"><div><h2>Create customer order</h2><p>Accepting an order reserves the BOM materials.</p></div><button class="secondary" type="button" (click)="loadOrders()">↻ Refresh</button></div>
          <form class="form-grid" (ngSubmit)="createOrder()">
            <label class="wide">Customer name *<input name="customerName" [(ngModel)]="orderForm.customerName" required></label>
            <label>Product *<select name="orderProduct" [(ngModel)]="orderForm.productId" required><option [ngValue]="null">Select product…</option><option *ngFor="let p of products" [ngValue]="p.id">{{p.code}} — {{p.name}}</option></select></label>
            <label>Quantity *<input name="orderQuantity" type="number" min="0.0001" step="0.0001" [(ngModel)]="orderForm.quantity" required></label>
            <div class="form-actions"><button class="primary" [disabled]="saving" type="submit">{{saving ? 'Creating…' : 'Create order & reserve materials'}}</button></div>
          </form>
        </section>
        <section class="panel section-gap">
          <div class="panel-head"><div><h2>Customer orders</h2><p>{{orders.length}} order(s) · Accepted orders can be sent to production.</p></div></div>
          <div class="empty" *ngIf="!orders.length">No orders yet. Create an order above.</div>
          <div class="table-wrap" *ngIf="orders.length"><table><thead><tr><th>ORDER</th><th>CUSTOMER</th><th>PRODUCTS</th><th>DATE</th><th>STATUS</th></tr></thead><tbody><tr *ngFor="let o of orders"><td><b>{{o.orderNumber}}</b></td><td>{{o.customerName}}</td><td><span *ngFor="let l of o.lines; let last=last">{{l.productName}} × {{l.quantity}}{{last ? '' : ', '}}</span></td><td>{{o.createdAt | date:'mediumDate'}}</td><td><span class="status">{{o.status}}</span></td></tr></tbody></table></div>
        </section>
      </ng-container>

      <!-- PRODUCTION -->
      <ng-container *ngIf="section==='production'">
        <section class="metrics">
          <article class="metric"><span class="metric-label">Production runs</span><strong>{{productionRuns.length}}</strong><small>All recorded runs</small></article>
          <article class="metric"><span class="metric-label">In progress</span><strong>{{countRuns('InProgress')}}</strong><small>Awaiting completion</small></article>
          <article class="metric"><span class="metric-label">Completed</span><strong>{{countRuns('Completed')}}</strong><small>Completed runs</small></article>
          <article class="metric"><span class="metric-label">Finished product types</span><strong>{{finishedGoods.length}}</strong><small>Products in stock ledger</small></article>
        </section>
        <section class="panel">
          <div class="panel-head"><div><h2>Start production</h2><p>Select an accepted order and product. BOM consumption is prepared automatically.</p></div><button class="secondary" type="button" (click)="loadProductionData()">↻ Refresh</button></div>
          <form class="form-grid" (ngSubmit)="startProduction()">
            <label class="wide">Customer order *<select name="productionOrder" [(ngModel)]="productionForm.orderId" (ngModelChange)="onProductionOrderChange()" required><option [ngValue]="null">Select order…</option><option *ngFor="let o of eligibleOrders" [ngValue]="o.id">{{o.orderNumber}} — {{o.customerName}} ({{o.status}})</option></select></label>
            <label>Product in order *<select name="productionProduct" [(ngModel)]="productionForm.productId" required><option [ngValue]="null">Select product…</option><option *ngFor="let l of selectedOrderLines" [ngValue]="l.productId">{{l.productCode}} — {{l.productName}} (ordered {{l.quantity}})</option></select></label>
            <label>Planned quantity *<input name="plannedProductionQuantity" type="number" min="0.0001" step="0.0001" [(ngModel)]="productionForm.plannedQuantity" required></label>
            <div class="form-actions"><button class="primary" type="submit" [disabled]="saving">{{saving ? 'Starting…' : 'Start production'}}</button></div>
          </form>
        </section>
        <section class="panel section-gap">
          <div class="panel-head"><div><h2>Production runs</h2><p>Open an in-progress run to enter actual material consumption and complete it.</p></div></div>
          <div class="empty" *ngIf="!productionRuns.length">No production runs yet.</div>
          <div class="table-wrap" *ngIf="productionRuns.length"><table><thead><tr><th>PRODUCTION</th><th>ORDER</th><th>PRODUCT</th><th>PLANNED</th><th>COMPLETED</th><th>STATUS</th><th>ACTION</th></tr></thead><tbody><tr *ngFor="let run of productionRuns"><td><b>{{run.productionNumber}}</b><small>{{run.startedAt | date:'short'}}</small></td><td>{{run.orderNumber || ('Order #' + run.orderId)}}</td><td>{{run.productName}}</td><td>{{run.plannedQuantity}}</td><td>{{run.completedQuantity}}</td><td><span class="status" [class.low]="run.status==='InProgress'">{{run.status}}</span></td><td><button *ngIf="run.status==='InProgress'" type="button" class="primary compact" (click)="selectProductionRun(run.id)">Record usage</button><button *ngIf="run.status==='InProgress'" type="button" class="danger" (click)="cancelProduction(run.id)">Cancel</button></td></tr></tbody></table></div>
        </section>
        <section class="panel section-gap" *ngIf="selectedRun">
          <div class="panel-head"><div><h2>Complete {{selectedRun.productionNumber}}</h2><p>Enter the actual quantity used for each material. Values default to the planned quantity.</p></div><button class="secondary" type="button" (click)="selectedRun=null">Close</button></div>
          <form class="form-grid" (ngSubmit)="completeProduction()">
            <label>Finished quantity *<input name="completedQuantity" type="number" min="0.0001" step="0.0001" [(ngModel)]="completeForm.completedQuantity" required></label>
            <div class="wide table-wrap"><table><thead><tr><th>PLANNED BOM PART</th><th>PLANNED QTY</th><th>ACTUAL COMPONENT USED *</th><th>ACTUAL QTY *</th></tr></thead><tbody><tr *ngFor="let c of selectedRun.consumptions"><td><b>{{c.partName || partName(c.partId)}}</b><small>{{partCode(c.partId)}} · {{partUnit(c.partId)}}</small></td><td>{{c.plannedQuantity}}</td><td><select class="quantity-input" [name]="'actual-part-' + c.partId" [(ngModel)]="c.actualPartId" required><option *ngFor="let p of parts" [ngValue]="p.id">{{p.code}} — {{p.name}} ({{p.unit}})</option></select><small *ngIf="c.actualPartId && c.actualPartId !== c.partId">Replacement component selected; available stock will be checked on completion.</small></td><td><input class="quantity-input" [name]="'actual-' + c.partId" type="number" min="0" step="0.0001" [max]="c.plannedQuantity" [(ngModel)]="c.actualQuantity" required></td></tr></tbody></table></div>
            <div class="form-actions"><button class="primary" type="submit" [disabled]="saving">{{saving ? 'Completing…' : 'Complete production & update stock'}}</button></div>
          </form>
        </section>
        <section class="panel section-gap"><div class="panel-head"><div><h2>Finished goods inventory</h2><p>Production increases stock; recording a delivery decreases it.</p></div><button class="secondary" type="button" (click)="loadFinishedGoods()">↻ Refresh</button></div><div class="empty" *ngIf="!finishedGoods.length">No finished-goods movements yet.</div><div class="table-wrap" *ngIf="finishedGoods.length"><table><thead><tr><th>CODE</th><th>PRODUCT</th><th>SELLING PRICE</th><th>PHYSICAL STOCK</th><th>DELIVERY QUANTITY</th><th>REFERENCE / NOTES</th><th>ACTION</th></tr></thead><tbody><tr *ngFor="let g of finishedGoods"><td><b>{{g.code}}</b></td><td>{{g.name}}</td><td>₹{{g.sellingPrice | number:'1.2-2'}}</td><td><b>{{g.physicalStock | number:'1.0-4'}}</b></td><td><input class="quantity-input" [name]="'delivery-qty-' + g.id" type="number" min="1" step="1" [max]="g.physicalStock" [(ngModel)]="g.deliveryQuantity" [disabled]="g.physicalStock <= 0" placeholder="Qty (whole units)"></td><td><input [name]="'delivery-ref-' + g.id" [(ngModel)]="g.deliveryReference" placeholder="Order / delivery ref"><input [name]="'delivery-notes-' + g.id" [(ngModel)]="g.deliveryNotes" placeholder="Notes (optional)"></td><td><button class="primary compact" type="button" (click)="markDelivered(g)" [disabled]="saving || g.physicalStock <= 0">Mark delivered</button></td></tr></tbody></table></div></section>
      </ng-container>

      <!-- PURCHASE RECEIVING -->
      <section class="panel" *ngIf="section==='purchases'">
        <div class="panel-head">
          <div>
            <h2>Receive purchased stock</h2>
            <p>Receiving a purchase increases physical stock through a stock movement.</p>
          </div>
        </div>

        <form class="form-grid" (ngSubmit)="receiveStock()">

          <label class="wide">
            Component / part *
            <select name="receivePart"
              [(ngModel)]="receiveForm.partId" required>
              <option [ngValue]="null">Select a part…</option>
              <option *ngFor="let part of parts" [ngValue]="part.id">
                {{ part.code }} — {{ part.name }} ({{ part.unit }})
              </option>
            </select>
          </label>

          <label>
            Quantity received *
            <input name="receiveQuantity" type="number"
              min="0.0001" step="0.0001"
              [(ngModel)]="receiveForm.quantity" required>
          </label>

          <label>
            Unit purchase cost (₹) *
            <input name="receiveCost" type="number"
              min="0" step="0.01"
              [(ngModel)]="receiveForm.unitCost" required>
          </label>

          <label>
            Supplier
            <input name="receiveSupplier"
              [(ngModel)]="receiveForm.supplier">
          </label>

          <label>
            Invoice / reference number
            <input name="receiveReference"
              [(ngModel)]="receiveForm.reference">
          </label>

          <label class="wide">
            Notes
            <input name="receiveNotes"
              [(ngModel)]="receiveForm.notes">
          </label>

          <div class="form-actions">
            <button class="primary" type="submit" [disabled]="saving">
              {{ saving ? 'Recording…' : 'Receive stock' }}
            </button>
            <button class="secondary" type="button"
              (click)="resetReceiveForm()">Clear form</button>
          </div>
        </form>
      </section>

      <footer>
        FOGERITHM ADMIN
        <span>Manual entry · Local database · Stock integrity by design</span>
      </footer>
    </main>
  </div>
  `
})
export class AppComponent implements OnInit {
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);
  private readonly api = 'http://localhost:5080/api';

  section = 'overview';

  inventory: InventoryRow[] = [];
  parts: Part[] = [];
  products: Product[] = [];
  orders: CustomerOrder[] = [];
  productionRuns: ProductionRun[] = [];
  finishedGoods: FinishedGood[] = [];
  selectedRun: ProductionRun | null = null;

  orderForm = { customerName: '', productId: null as number | null, quantity: 1 };
  productionForm = { orderId: null as number | null, productId: null as number | null, plannedQuantity: 1 };
  completeForm = { completedQuantity: 1 };

  partForm: Partial<Part> = emptyPart();
  productForm: Partial<Product> = emptyProduct();

  editingPartId: number | null = null;

  receiveForm = {
    partId: null as number | null,
    quantity: 1,
    unitCost: 0,
    supplier: '',
    reference: '',
    notes: ''
  };

  adjustmentForm = {
    partId: null as number | null,
    quantityChange: 0,
    reason: ''
  };

  selectedProductId: number | null = null;
  bomPartId: number | null = null;
  bomQuantity = 1;

  saving = false;
  loading = false;
  error = '';
  notice = '';

  get pageTitle() {
    const titles: Record<string, string> = {
      overview: 'Overview',
      inventory: 'Parts & Inventory',
      products: 'Products & BOM',
      orders: 'Customer Orders',
      production: 'Production',
      purchases: 'Purchases'
    };

    return titles[this.section] || 'Overview';
  }

  get attentionCount() {
    return this.inventory.filter(x => x.stockStatus !== 'OK').length;
  }

  get totalPhysical() {
    return this.inventory.reduce(
      (a, x) => a + Number(x.physicalStock || 0), 0
    );
  }

  get totalReserved() {
    return this.inventory.reduce(
      (a, x) => a + Number(x.reservedStock || 0), 0
    );
  }

  get selectedProduct() {
    return this.products.find(p => p.id === Number(this.selectedProductId));
  }

  ngOnInit() {
    this.refreshAll();
  }

  go(section: string) {
    this.section = section;
    this.error = '';
    this.notice = '';

    if (section === 'products') {
      this.loadParts();
      this.loadProducts();
    }

    if (section === 'inventory' || section === 'purchases') {
      this.loadParts();
      this.loadInventory();
    }
    if (section === 'orders') {
      this.loadProducts();
      this.loadOrders();
    }
    if (section === 'production') {
      this.loadProducts();
      this.loadOrders();
      this.loadProductionData();
    }
  }

  refreshAll() {
    this.loadInventory();
    this.loadParts();
    this.loadProducts();
    if (this.section === 'orders' || this.section === 'production') this.loadOrders();
    if (this.section === 'production') this.loadProductionData();
  }

  get eligibleOrders() {
    return this.orders.filter(o => o.status === 'Accepted' || o.status === 'InProduction');
  }

  get selectedOrderLines() {
    return this.orders.find(o => o.id === Number(this.productionForm.orderId))?.lines || [];
  }

  countRuns(status: string) {
    return this.productionRuns.filter(r => r.status === status).length;
  }

  loadOrders() {
    this.http.get<CustomerOrder[]>(`${this.api}/orders`).subscribe({
      next: rows => { this.orders = rows; this.cdr.markForCheck(); },
      error: e => this.showError(e, 'Could not load customer orders.')
    });
  }

  createOrder() {
    if (!this.orderForm.customerName.trim() || !this.orderForm.productId || this.orderForm.quantity <= 0) {
      this.error = 'Enter a customer name, select a product and enter a positive quantity.'; return;
    }
    this.saving = true; this.clearMessages();
    this.http.post<CustomerOrder>(`${this.api}/orders`, {
      customerName: this.orderForm.customerName.trim(),
      lines: [{ productId: this.orderForm.productId, quantity: Number(this.orderForm.quantity) }]
    }).subscribe({
      next: result => {
        this.saving = false; this.notice = `Order ${result.orderNumber} created and materials reserved.`;
        this.orderForm = { customerName: '', productId: null, quantity: 1 };
        this.loadOrders(); this.loadInventory();
      },
      error: e => { this.saving = false; this.showError(e, 'Could not create order. Check BOM and available stock.'); }
    });
  }

  onProductionOrderChange() { this.productionForm.productId = null; }

  loadProductionData() {
    this.http.get<ProductionRun[]>(`${this.api}/production`).subscribe({
      next: rows => { this.productionRuns = rows; this.cdr.markForCheck(); },
      error: e => this.showError(e, 'Could not load production runs.')
    });
    this.loadFinishedGoods();
  }

  loadFinishedGoods() {
    this.http.get<FinishedGood[]>(`${this.api}/finished-goods`).subscribe({
      next: rows => { this.finishedGoods = rows.map(g => ({ ...g, deliveryQuantity: g.deliveryQuantity && g.deliveryQuantity >= 1 ? g.deliveryQuantity : 1 })); this.cdr.markForCheck(); },
      error: e => this.showError(e, 'Could not load finished-goods inventory.')
    });
  }

  markDelivered(g: FinishedGood) {
    const quantity = Number(g.deliveryQuantity);
    if (!Number.isInteger(quantity) || quantity < 1) {
      this.error = 'Enter a whole-number delivery quantity of at least 1.';
      return;
    }
    if (quantity > g.physicalStock) {
      this.error = `Only ${g.physicalStock} unit(s) of ${g.name} are currently in stock.`;
      return;
    }

    if (!confirm(`Mark ${quantity} unit(s) of ${g.name} as delivered? Physical stock will decrease.`)) return;

    this.saving = true;
    this.clearMessages();
    this.http.post<any>(`${this.api}/finished-goods/deliver`, {
      productId: g.id,
      quantity,
      reference: g.deliveryReference || '',
      notes: g.deliveryNotes || ''
    }).subscribe({
      next: result => {
        this.saving = false;
        this.notice = result?.message || `Delivery recorded for ${g.name}.`;
        this.loadFinishedGoods();
        this.cdr.markForCheck();
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not record delivery. Refresh inventory and try again.');
      }
    });
  }

  startProduction() {
    const f = this.productionForm;
    if (!f.orderId || !f.productId || !Number.isFinite(Number(f.plannedQuantity)) || f.plannedQuantity <= 0) {
      this.error = 'Select an order and product and enter a positive planned quantity.'; return;
    }
    this.saving = true; this.clearMessages();
    this.http.post<ProductionRun>(`${this.api}/production`, {
      orderId: f.orderId, productId: f.productId, plannedQuantity: Number(f.plannedQuantity)
    }).subscribe({
      next: result => {
        this.saving = false; this.notice = `Production ${result.productionNumber} started.`;
        this.productionForm = { orderId: f.orderId, productId: f.productId, plannedQuantity: 1 };
        this.loadOrders(); this.loadProductionData(); this.loadInventory();
      },
      error: e => { this.saving = false; this.showError(e, 'Could not start production. Check order reservations and BOM.'); }
    });
  }

  selectProductionRun(id: number) {
    this.clearMessages();
    this.http.get<ProductionRun>(`${this.api}/production/${id}`).subscribe({
      next: run => {
        this.selectedRun = { ...run, consumptions: (run.consumptions || []).map(c => ({ ...c, actualPartId: c.actualPartId ?? c.partId, actualQuantity: c.plannedQuantity })) };
        this.completeForm = { completedQuantity: run.plannedQuantity };
        this.cdr.markForCheck();
      },
      error: e => this.showError(e, 'Could not load production details.')
    });
  }

  completeProduction() {
    const run = this.selectedRun;
    if (!run?.consumptions?.length || this.completeForm.completedQuantity <= 0) {
      this.error = 'Enter a valid completed quantity and material consumption.'; return;
    }
    if (run.consumptions.some(c => !Number.isFinite(Number(c.actualQuantity)) || c.actualQuantity < 0 || c.actualQuantity > c.plannedQuantity)) {
      this.error = 'Actual consumption must be between zero and the planned quantity for each material.'; return;
    }
    if (!confirm(`Complete ${run.productionNumber} and post material usage and finished goods to inventory?`)) return;
    this.saving = true; this.clearMessages();
    this.http.post(`${this.api}/production/${run.id}/complete`, {
      completedQuantity: Number(this.completeForm.completedQuantity),
      consumptions: run.consumptions.map(c => ({ partId: c.partId, actualPartId: Number(c.actualPartId || c.partId), actualQuantity: Number(c.actualQuantity) }))
    }).subscribe({
      next: () => {
        this.saving = false; this.notice = `Production ${run.productionNumber} completed. Inventory updated.`;
        this.selectedRun = null; this.loadProductionData(); this.loadOrders(); this.loadInventory();
      },
      error: e => { this.saving = false; this.showError(e, 'Could not complete production.'); }
    });
  }

  cancelProduction(id: number) {
    const run = this.productionRuns.find(x => x.id === id);
    if (!run || !confirm(`Cancel ${run.productionNumber}? Its material allocation will be released.`)) return;
    this.saving = true; this.clearMessages();
    this.http.post(`${this.api}/production/${id}/cancel`, {}).subscribe({
      next: () => { this.saving = false; this.notice = `Production ${run.productionNumber} cancelled.`; if (this.selectedRun?.id === id) this.selectedRun = null; this.loadProductionData(); this.loadOrders(); this.loadInventory(); },
      error: e => { this.saving = false; this.showError(e, 'Could not cancel production.'); }
    });
  }

  loadInventory() {
    this.http.get<InventoryRow[]>(`${this.api}/inventory`).subscribe({
      next: rows => {
        // Calculate status defensively as well as receiving it from the API.
        // This prevents every part being counted as low stock if an older API
        // response omits StockStatus.
        this.inventory = rows.map(row => {
          const available = Number(row.availableStock ?? 0);
          const reorder = Number(row.reorderLevel ?? 0);
          const stockStatus = available <= 0
            ? 'OUT OF STOCK'
            : available <= reorder
              ? 'LOW STOCK'
              : 'OK';
          return { ...row, stockStatus };
        });
        this.cdr.markForCheck();
      },
      error: e => this.showError(
        e, 'Could not load inventory. Check that the backend is running.'
      )
    });
  }

  loadParts() {
    this.http.get<Part[]>(`${this.api}/parts`).subscribe({
      next: rows => { this.parts = rows; this.cdr.markForCheck(); },
      error: e => this.showError(e, 'Could not load parts.')
    });
  }

  loadProducts() {
    this.http.get<Product[]>(`${this.api}/products`).subscribe({
      next: rows => {
        this.products = rows.map(p => ({
          ...p,
          bomLines: p.bomLines || []
        }));

        if (
          this.selectedProductId &&
          !this.products.some(p => p.id === this.selectedProductId)
        ) {
          this.selectedProductId = null;
        }
      },
      error: e => this.showError(e, 'Could not load products.')
    });
  }

  editPart(id: number) {
    const part = this.parts.find(p => p.id === id);

    if (!part) {
      this.error = 'Part not found. Refresh the inventory and try again.';
      return;
    }

    this.editingPartId = part.id;
    this.partForm = { ...part };
    this.clearMessages();

    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  savePart() {
    if (!this.partForm.code?.trim() || !this.partForm.name?.trim()) {
      this.error = 'Part code and name are required.';
      return;
    }

    const unitCost = Number(this.partForm.unitCost ?? 0);
    const reorderLevel = Number(this.partForm.reorderLevel ?? 0);
    const openingStock = Number(this.partForm.openingStock ?? 0);

    if (
      !Number.isFinite(unitCost) || unitCost < 0 ||
      !Number.isFinite(reorderLevel) || reorderLevel < 0 ||
      (!this.editingPartId &&
        (!Number.isFinite(openingStock) || openingStock < 0))
    ) {
      this.error = 'Cost, reorder level and opening stock must be valid non-negative numbers.';
      return;
    }

    this.saving = true;
    this.clearMessages();

    const payload = {
      ...this.partForm,
      code: this.partForm.code.trim(),
      name: this.partForm.name.trim(),
      unitCost,
      reorderLevel,
      ...(this.editingPartId ? {} : { openingStock })
    };

    const request = this.editingPartId
      ? this.http.put(
          `${this.api}/parts/${this.editingPartId}`,
          payload
        )
      : this.http.post<Part>(`${this.api}/parts`, payload);

    request.subscribe({
      next: () => {
        const wasEditing = this.editingPartId !== null;

        this.saving = false;
        this.notice = wasEditing
          ? 'Part details updated successfully.'
          : 'Part saved successfully.';

        this.resetPart();
        this.refreshAll();
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not save the part.');
      }
    });
  }

  resetPart() {
    this.partForm = emptyPart();
    this.editingPartId = null;
    this.clearMessages();
  }

  deletePart(id: number) {
    const part = this.parts.find(p => p.id === id);

    if (!part) {
      this.error = 'Part not found.';
      return;
    }

    if (!confirm(
      `Delete ${part.code} — ${part.name}? This is only allowed when the part has no dependent records or stock history.`
    )) {
      return;
    }

    this.saving = true;
    this.clearMessages();

    this.http.delete(`${this.api}/parts/${id}`).subscribe({
      next: () => {
        this.saving = false;
        this.notice = `Part ${part.code} deleted successfully.`;

        if (this.editingPartId === id) {
          this.partForm = emptyPart();
          this.editingPartId = null;
        }

        this.refreshAll();
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not delete the part.');
      }
    });
  }

  receiveStock() {
    const f = this.receiveForm;

    if (
      !f.partId ||
      !Number.isFinite(Number(f.quantity)) ||
      f.quantity <= 0 ||
      !Number.isFinite(Number(f.unitCost)) ||
      f.unitCost < 0
    ) {
      this.error = 'Choose a part, enter a positive quantity, and enter a valid unit cost.';
      return;
    }

    this.saving = true;
    this.clearMessages();

    this.http.post(`${this.api}/stock/receive`, {
      partId: f.partId,
      quantity: f.quantity,
      unitCost: f.unitCost,
      supplier: f.supplier,
      reference: f.reference,
      notes: f.notes
    }).subscribe({
      next: () => {
        this.saving = false;
        this.notice = 'Purchase received. Physical stock has been updated.';
        this.resetReceiveForm();
        this.refreshAll();
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not receive stock.');
      }
    });
  }

  resetReceiveForm() {
    this.receiveForm = {
      partId: null,
      quantity: 1,
      unitCost: 0,
      supplier: '',
      reference: '',
      notes: ''
    };

    this.clearMessages();
  }

  adjustStock() {
    const f = this.adjustmentForm;

    if (
      !f.partId ||
      !Number.isFinite(Number(f.quantityChange)) ||
      f.quantityChange === 0 ||
      !f.reason.trim()
    ) {
      this.error = 'Choose a part, enter a non-zero quantity change, and provide a reason.';
      return;
    }

    if (!confirm(
      `Record a stock adjustment of ${f.quantityChange}? Check the quantity and reason before continuing.`
    )) {
      return;
    }

    this.saving = true;
    this.clearMessages();

    this.http.post(`${this.api}/stock/adjust`, {
      partId: f.partId,
      quantityChange: f.quantityChange,
      reason: f.reason.trim()
    }).subscribe({
      next: () => {
        this.saving = false;
        this.notice = 'Stock adjustment recorded successfully.';

        this.adjustmentForm = {
          partId: null,
          quantityChange: 0,
          reason: ''
        };

        this.refreshAll();
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not adjust stock.');
      }
    });
  }

  saveProduct() {
    if (
      !this.productForm.code?.trim() ||
      !this.productForm.name?.trim()
    ) {
      this.error = 'Product code and name are required.';
      return;
    }

    this.saving = true;
    this.clearMessages();

    const payload = {
      ...this.productForm,
      code: this.productForm.code.trim(),
      name: this.productForm.name.trim()
    };

    this.http.post<Product>(
      `${this.api}/products`, payload
    ).subscribe({
      next: saved => {
        this.saving = false;
        this.notice = `Product ${saved.code} saved. Now add its BOM lines.`;
        this.productForm = emptyProduct();
        this.refreshAll();
        this.selectedProductId = saved.id;
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not save the product.');
      }
    });
  }

  resetProduct() {
    this.productForm = emptyProduct();
    this.clearMessages();
  }

  onSelectedProductChange() {
    this.bomPartId = null;
    this.bomQuantity = 1;
  }

  addBomLine() {
    if (
      !this.selectedProductId ||
      !this.bomPartId ||
      !Number(this.bomQuantity) ||
      this.bomQuantity <= 0
    ) {
      this.error = 'Choose a product, a component, and a quantity greater than zero.';
      return;
    }

    this.saving = true;
    this.clearMessages();

    this.http.post<BomLine>(
      `${this.api}/products/${this.selectedProductId}/bom-lines`,
      {
        partId: this.bomPartId,
        quantityPerProduct: this.bomQuantity
      }
    ).subscribe({
      next: () => {
        this.saving = false;
        this.notice = 'BOM line added.';
        this.bomPartId = null;
        this.bomQuantity = 1;
        this.loadProducts();
      },
      error: e => {
        this.saving = false;
        this.showError(e, 'Could not add BOM line.');
      }
    });
  }

  removeBomLine(lineId: number) {
    if (!confirm('Remove this component from the product BOM?')) {
      return;
    }

    this.http.delete(`${this.api}/bom-lines/${lineId}`).subscribe({
      next: () => {
        this.notice = 'BOM line removed.';
        this.loadProducts();
      },
      error: e => this.showError(e, 'Could not remove BOM line.')
    });
  }

  partCode(id: number) {
    return this.parts.find(p => p.id === id)?.code || `PART-${id}`;
  }

  partName(id: number) {
    return this.parts.find(p => p.id === id)?.name || 'Unknown part';
  }

  partUnit(id: number) {
    return this.parts.find(p => p.id === id)?.unit || '';
  }

  private clearMessages() {
    this.error = '';
    this.notice = '';
  }

  private showError(e: any, fallback: string) {
    const message = e?.error?.message || e?.error?.title || fallback;
    this.error = message;
    this.cdr.markForCheck();
  }
}
