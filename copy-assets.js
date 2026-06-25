const fs = require('fs');
const path = require('path');

// Helper to copy file
function copyFile(src, dest) {
  const destDir = path.dirname(dest);
  if (!fs.existsSync(destDir)) {
    fs.mkdirSync(destDir, { recursive: true });
  }
  fs.copyFileSync(src, dest);
  console.log(`Copied: ${src} -> ${dest}`);
}

// Helper to copy directory recursively
function copyDir(src, dest) {
  if (!fs.existsSync(src)) return;
  if (!fs.existsSync(dest)) {
    fs.mkdirSync(dest, { recursive: true });
  }
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (let entry of entries) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDir(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
  console.log(`Copied directory: ${src} -> ${dest}`);
}

const assets = {
  // Bootstrap 5
  'node_modules/bootstrap/dist/css/bootstrap.min.css': 'assets/css/bootstrap.min.css',
  'node_modules/bootstrap/dist/css/bootstrap.rtl.min.css': 'assets/css/bootstrap.rtl.min.css',
  'node_modules/bootstrap/dist/js/bootstrap.bundle.min.js': 'assets/js/bootstrap.bundle.min.js',

  // Bootstrap Icons
  'node_modules/bootstrap-icons/font/bootstrap-icons.min.css': 'assets/css/bootstrap-icons.min.css',
  'node_modules/bootstrap-icons/font/fonts/bootstrap-icons.woff2': 'assets/css/fonts/bootstrap-icons.woff2',
  'node_modules/bootstrap-icons/font/fonts/bootstrap-icons.woff': 'assets/css/fonts/bootstrap-icons.woff',

  // ApexCharts
  'node_modules/apexcharts/dist/apexcharts.min.js': 'assets/js/apexcharts.min.js',

  // SortableJS
  'node_modules/sortablejs/Sortable.min.js': 'assets/js/sortable.min.js',

  // bpmn-js
  'node_modules/bpmn-js/dist/bpmn-modeler.production.min.js': 'assets/js/bpmn-modeler.production.min.js',
  'node_modules/bpmn-js/dist/assets/bpmn-js.css': 'assets/css/bpmn-js.css',
  'node_modules/bpmn-js/dist/assets/diagram-js.css': 'assets/css/diagram-js.css',
};

// Check if ApexCharts CSS exists (some versions have it, let's copy if present)
const apexCss = 'node_modules/apexcharts/dist/apexcharts.css';
if (fs.existsSync(apexCss)) {
  assets[apexCss] = 'assets/css/apexcharts.css';
}

// Copy single files
Object.entries(assets).forEach(([src, dest]) => {
  if (fs.existsSync(src)) {
    copyFile(src, dest);
  } else {
    console.error(`Warning: Source file not found: ${src}`);
  }
});

// Copy bpmn-js fonts
copyDir('node_modules/bpmn-js/dist/assets/bpmn-font', 'assets/css/bpmn-font');

// Copy specific subsetted font files from @fontsource
const fonts = [
  // Cairo
  { src: 'node_modules/@fontsource/cairo/files/cairo-arabic-400-normal.woff2', dest: 'assets/fonts/cairo/cairo-arabic-400-normal.woff2' },
  { src: 'node_modules/@fontsource/cairo/files/cairo-arabic-600-normal.woff2', dest: 'assets/fonts/cairo/cairo-arabic-600-normal.woff2' },
  { src: 'node_modules/@fontsource/cairo/files/cairo-arabic-700-normal.woff2', dest: 'assets/fonts/cairo/cairo-arabic-700-normal.woff2' },
  { src: 'node_modules/@fontsource/cairo/files/cairo-latin-400-normal.woff2', dest: 'assets/fonts/cairo/cairo-latin-400-normal.woff2' },
  { src: 'node_modules/@fontsource/cairo/files/cairo-latin-700-normal.woff2', dest: 'assets/fonts/cairo/cairo-latin-700-normal.woff2' },
  // Inter
  { src: 'node_modules/@fontsource/inter/files/inter-latin-400-normal.woff2', dest: 'assets/fonts/inter/inter-latin-400-normal.woff2' },
  { src: 'node_modules/@fontsource/inter/files/inter-latin-600-normal.woff2', dest: 'assets/fonts/inter/inter-latin-600-normal.woff2' },
  { src: 'node_modules/@fontsource/inter/files/inter-latin-700-normal.woff2', dest: 'assets/fonts/inter/inter-latin-700-normal.woff2' },
];

fonts.forEach(f => {
  if (fs.existsSync(f.src)) {
    copyFile(f.src, f.dest);
  } else {
    // If exact name is slightly different, look for pattern or print warning
    console.warn(`Font file not found: ${f.src}`);
  }
});

console.log('Asset copying completed!');
