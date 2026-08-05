import type { FeatureCollection, Geometry } from 'geojson'
import type { components } from './schema'

/**
 * Tipovi API odgovora dolaze iz generisane sheme (`schema.ts`), koja se pravi iz
 * OpenAPI dokumenta backenda: `npm run generate:api`.
 *
 * Ovdje se ništa ne opisuje ručno — samo se generisanim tipovima daju upotrebljiva imena.
 * Ručno pisan interfejs bi se prije ili kasnije razišao sa onim što server stvarno šalje.
 */
export type ReachProperties = components['schemas']['ReachProperties']
export type ReachThreshold = components['schemas']['ReachThreshold']
export type ReachMeta = components['schemas']['ReachMeta']
export type SourceStatus = components['schemas']['SourceStatus']
export type StationProperties = components['schemas']['StationProperties']
export type StationMeta = components['schemas']['StationMeta']

/** GeoJSON kolekcija dionica, sa `meta` zaglavljem koje backend dodaje. */
export type ReachCollection = FeatureCollection<Geometry, ReachProperties> & {
  meta: ReachMeta
}

/**
 * Registar mjernih mjesta. Zaseban tip jer je zaseban sloj — dionice nose stanje,
 * stanice nose samo lokaciju (SOURCES.md §1.7).
 */
export type StationCollection = FeatureCollection<Geometry, StationProperties> & {
  meta: StationMeta
}
