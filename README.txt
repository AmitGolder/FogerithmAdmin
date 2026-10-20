FOGERITHM ADMIN — MANUAL ENTRY PATCH

This patch intentionally does NOT use Excel import. It adds manual entry for:
- Parts/raw materials, including opening physical stock and reorder level
- Finished products
- Product BOM lines (one component per row)
- Removing BOM lines (blocked if the component has active reservations)

No database migration is required because these API routes use existing tables.

Before replacing files, back up your current versions.

Copy these files into your existing project:
1) frontend/src/app/app.component.ts
   -> C:\FogerithmAdmin\FogerithmAdminStarter\frontend\src\app\app.component.ts
2) frontend/src/styles.css
   -> C:\FogerithmAdmin\FogerithmAdminStarter\frontend\src\styles.css
3) backend/Fogerithm.Api/Program.cs
   -> C:\FogerithmAdmin\FogerithmAdminStarter\backend\Fogerithm.Api\Program.cs

Then stop the running frontend/backend terminals with Ctrl+C.

Backend terminal:
  cd C:\FogerithmAdmin\FogerithmAdminStarter\backend\Fogerithm.Api
  dotnet build
  dotnet run --no-launch-profile --urls http://localhost:5080

Frontend terminal:
  cd C:\FogerithmAdmin\FogerithmAdminStarter\frontend
  npm.cmd run build
  npm.cmd start

Open http://localhost:4200

Setup sequence:
1. Parts & Inventory: enter each component. Opening physical stock is current stock at setup; use zero if none.
2. Products & BOM: create the finished product.
3. Select the product and add one BOM line per required component, with quantity needed for one finished unit.
4. Verify the BOM lines and inventory before entering orders.

Stock rule: accepting orders reserves components; it must not reduce physical stock. Physical stock changes only through stock movements / production consumption. Those workflows are future implementation steps.
