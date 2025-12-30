type MetaDescriptor = {
  title?: string
  description?: string
  keywords?: string
  url?: string
  image?: string
  type?: string
  locale?: string
  robots?: string
}

const defaultTitle = 'TripShare Sri Lanka | Verified Carpool & Ride Sharing'
const defaultDescription =
  'Find trusted carpools and shared rides across Sri Lanka with verified drivers, live ETAs, and safety sharing.'
const defaultKeywords =
  'Sri Lanka carpool, ride sharing Sri Lanka, Colombo carpool, Kandy rides, Galle rides, TripShare, verified drivers, book seats, live ETA'

function upsertMeta(attr: 'name' | 'property', key: string, value: string | undefined) {
  if (!value) return
  let el = document.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.setAttribute('content', value)
}

function upsertLink(rel: string, href: string | undefined) {
  if (!href) return
  let el = document.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`)
  if (!el) {
    el = document.createElement('link')
    el.setAttribute('rel', rel)
    document.head.appendChild(el)
  }
  el.setAttribute('href', href)
}

export function applySeo(meta: MetaDescriptor) {
  const title = meta.title || defaultTitle
  const description = meta.description || defaultDescription
  const keywords = meta.keywords || defaultKeywords
  const url = meta.url || window.location.href
  const image = meta.image || '/branding/hero.svg'
  const type = meta.type || 'website'
  const robots = meta.robots || 'index,follow'

  document.title = title
  upsertMeta('name', 'description', description)
  upsertMeta('name', 'keywords', keywords)
  upsertMeta('name', 'robots', robots)
  upsertMeta('property', 'og:title', title)
  upsertMeta('property', 'og:description', description)
  upsertMeta('property', 'og:type', type)
  upsertMeta('property', 'og:url', url)
  upsertMeta('property', 'og:image', image)
  upsertMeta('name', 'twitter:card', 'summary_large_image')
  upsertMeta('name', 'twitter:title', title)
  upsertMeta('name', 'twitter:description', description)
  upsertMeta('name', 'twitter:image', image)
  upsertLink('canonical', url.split('#')[0])
}

export function upsertLdJson(id: string, data: Record<string, any>) {
  let el = document.querySelector<HTMLScriptElement>(`script[data-seo="${id}"]`)
  if (!el) {
    el = document.createElement('script')
    el.type = 'application/ld+json'
    el.setAttribute('data-seo', id)
    document.head.appendChild(el)
  }
  el.textContent = JSON.stringify(data)
}
