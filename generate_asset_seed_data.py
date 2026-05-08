from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill
from datetime import datetime

# Create workbook
wb = Workbook()
ws = wb.active
ws.title = 'Asset Import Template'

# Headers
headers = [
    'Asset Code*', 'Name*', 'Description', 'Category Code*',
    'Purchase Date*', 'Purchase Price*', 'Installation Cost',
    'Tax Amount', 'Useful Life (Months)*', 'Residual Value', 'Serial Number'
]

for col, header in enumerate(headers, 1):
    cell = ws.cell(1, col, header)
    cell.font = Font(bold=True)
    cell.fill = PatternFill(start_color='ADD8E6', end_color='ADD8E6', fill_type='solid')

# Sample asset data
assets = [
    # Computer Hardware
    {
        'code': 'FA-COMP-001',
        'name': 'Dell Latitude 5520 Laptop',
        'desc': 'Intel Core i7, 16GB RAM, 512GB SSD',
        'cat': 'COMP-HW',
        'date': '2024-01-15',
        'price': 1500.00,
        'install': 50.00,
        'tax': 195.00,
        'life': 36,
        'residual': 150.00,
        'serial': 'DL5520-2024-001'
    },
    {
        'code': 'FA-COMP-002',
        'name': 'HP EliteDesk 800 Desktop',
        'desc': 'Intel Core i5, 8GB RAM, 256GB SSD',
        'cat': 'COMP-HW',
        'date': '2024-01-20',
        'price': 950.00,
        'install': 30.00,
        'tax': 127.40,
        'life': 48,
        'residual': 100.00,
        'serial': 'HP800-2024-002'
    },
    {
        'code': 'FA-COMP-003',
        'name': 'MacBook Pro 14-inch',
        'desc': 'M2 Pro, 16GB RAM, 512GB SSD',
        'cat': 'COMP-HW',
        'date': '2024-02-01',
        'price': 2499.00,
        'install': 0.00,
        'tax': 324.87,
        'life': 36,
        'residual': 500.00,
        'serial': 'MBP14-2024-003'
    },
    {
        'code': 'FA-COMP-004',
        'name': 'Dell UltraSharp Monitor 27"',
        'desc': '4K display, USB-C hub',
        'cat': 'COMP-HW',
        'date': '2024-01-30',
        'price': 550.00,
        'install': 0.00,
        'tax': 71.50,
        'life': 48,
        'residual': 50.00,
        'serial': 'DL-US27-004'
    },
    {
        'code': 'FA-COMP-005',
        'name': 'Lenovo ThinkPad X1 Carbon',
        'desc': 'Intel i7, 16GB RAM, 1TB SSD',
        'cat': 'COMP-HW',
        'date': '2024-02-05',
        'price': 1899.00,
        'install': 40.00,
        'tax': 252.07,
        'life': 36,
        'residual': 200.00,
        'serial': 'LEN-X1C-005'
    },
    # Furniture
    {
        'code': 'FA-FURN-001',
        'name': 'Executive Office Desk',
        'desc': 'Mahogany wood, L-shaped, 72x72 inches',
        'cat': 'FURN',
        'date': '2023-11-10',
        'price': 850.00,
        'install': 100.00,
        'tax': 123.50,
        'life': 84,
        'residual': 50.00,
        'serial': 'DESK-EXE-001'
    },
    {
        'code': 'FA-FURN-002',
        'name': 'Ergonomic Office Chair',
        'desc': 'Herman Miller Aeron, Size B',
        'cat': 'FURN',
        'date': '2023-11-10',
        'price': 1200.00,
        'install': 0.00,
        'tax': 156.00,
        'life': 60,
        'residual': 100.00,
        'serial': 'HM-AERON-002'
    },
    {
        'code': 'FA-FURN-003',
        'name': 'Conference Table',
        'desc': '12-person capacity, solid oak',
        'cat': 'FURN',
        'date': '2023-12-05',
        'price': 2500.00,
        'install': 200.00,
        'tax': 351.00,
        'life': 120,
        'residual': 200.00,
        'serial': 'CONF-TBL-003'
    },
    {
        'code': 'FA-FURN-004',
        'name': 'Filing Cabinet 4-Drawer',
        'desc': 'Steel, lockable, letter size',
        'cat': 'FURN',
        'date': '2023-11-15',
        'price': 350.00,
        'install': 0.00,
        'tax': 45.50,
        'life': 84,
        'residual': 25.00,
        'serial': 'FILE-4D-004'
    },
    # Vehicles
    {
        'code': 'FA-VEH-001',
        'name': 'Toyota Camry 2024',
        'desc': 'Sedan, 4-door, automatic transmission',
        'cat': 'VEH',
        'date': '2024-01-05',
        'price': 28500.00,
        'install': 500.00,
        'tax': 3770.00,
        'life': 60,
        'residual': 8000.00,
        'serial': 'TOY-CAM-2024-001'
    },
    {
        'code': 'FA-VEH-002',
        'name': 'Ford F-150 Pickup',
        'desc': 'Crew cab, 4WD, V8 engine',
        'cat': 'VEH',
        'date': '2024-02-10',
        'price': 45000.00,
        'install': 800.00,
        'tax': 5954.00,
        'life': 60,
        'residual': 12000.00,
        'serial': 'FORD-F150-2024-002'
    },
    # Equipment
    {
        'code': 'FA-EQUIP-001',
        'name': 'HP LaserJet Pro Printer',
        'desc': 'Color laser, duplex, network',
        'cat': 'EQUIP',
        'date': '2024-01-25',
        'price': 650.00,
        'install': 25.00,
        'tax': 87.75,
        'life': 48,
        'residual': 50.00,
        'serial': 'HP-LJ-PRO-001'
    },
    {
        'code': 'FA-EQUIP-002',
        'name': 'Cisco Network Switch',
        'desc': '24-port gigabit managed switch',
        'cat': 'EQUIP',
        'date': '2024-02-15',
        'price': 1200.00,
        'install': 150.00,
        'tax': 175.50,
        'life': 60,
        'residual': 100.00,
        'serial': 'CISCO-SW24-002'
    },
    {
        'code': 'FA-EQUIP-003',
        'name': 'Canon EOS R6 Camera',
        'desc': 'Mirrorless, 24-105mm lens kit',
        'cat': 'EQUIP',
        'date': '2023-12-20',
        'price': 2899.00,
        'install': 0.00,
        'tax': 376.87,
        'life': 48,
        'residual': 500.00,
        'serial': 'CANON-R6-003'
    },
    # Building Improvements
    {
        'code': 'FA-BUILD-001',
        'name': 'Office Building Renovation',
        'desc': 'Floor 3 renovation and modernization',
        'cat': 'BUILD',
        'date': '2023-10-01',
        'price': 125000.00,
        'install': 15000.00,
        'tax': 18200.00,
        'life': 240,
        'residual': 10000.00,
        'serial': 'RENO-FL3-001'
    }
]

# Add data rows
for row, asset in enumerate(assets, 2):
    ws.cell(row, 1, asset['code'])
    ws.cell(row, 2, asset['name'])
    ws.cell(row, 3, asset['desc'])
    ws.cell(row, 4, asset['cat'])
    ws.cell(row, 5, asset['date'])
    ws.cell(row, 6, asset['price'])
    ws.cell(row, 7, asset['install'])
    ws.cell(row, 8, asset['tax'])
    ws.cell(row, 9, asset['life'])
    ws.cell(row, 10, asset['residual'])
    ws.cell(row, 11, asset['serial'])

# Auto-fit columns
for col in range(1, 12):
    ws.column_dimensions[chr(64+col)].width = 22

# Save file
output_path = 'C:/Users/Akwas/Downloads/FixedAssets_SeedData.xlsx'
wb.save(output_path)
print(f'✓ Excel file created successfully!')
print(f'✓ Location: {output_path}')
print(f'✓ Total assets: {len(assets)}')
print(f'✓ Categories: COMP-HW, FURN, VEH, EQUIP, BUILD')
