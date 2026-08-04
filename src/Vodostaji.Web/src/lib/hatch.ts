/**
 * Dijagonalna šrafura za dionice bez podatka.
 *
 * UI.md §2 je izričit: **šrafura nije dekoracija.** Siva ispuna sama može izgledati kao
 * "mirno" — šrafura ne može. Ovo je vizuelna implementacija zlatnog pravila 1, i zato
 * se ne izostavlja ni kad izgleda "bučno".
 *
 * MapLibre `fill-pattern` traži sliku, pa se crta na canvasu i registruje u stilu.
 */
export function createHatchPattern(size = 12): ImageData {
  const canvas = document.createElement('canvas')
  canvas.width = size
  canvas.height = size

  const context = canvas.getContext('2d')
  if (!context) {
    throw new Error('Canvas 2D kontekst nije dostupan — šrafura se ne može nacrtati.')
  }

  // Siva iz legende agencije (`No Data` → #CCCCCC), pa preko nje tamnije linije.
  context.fillStyle = '#cccccc'
  context.fillRect(0, 0, size, size)

  context.strokeStyle = '#5c6470'
  context.lineWidth = 2.5
  context.lineCap = 'square'

  // Dvije linije da se obrazac nastavi preko ivica pločice.
  context.beginPath()
  context.moveTo(-size / 2, size / 2)
  context.lineTo(size / 2, -size / 2)
  context.moveTo(0, size)
  context.lineTo(size, 0)
  context.moveTo(size / 2, size + size / 2)
  context.lineTo(size + size / 2, size / 2)
  context.stroke()

  return context.getImageData(0, 0, size, size)
}
