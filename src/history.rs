/// Module de gestion de l'historique de navigation.
/// L'accès au store passe par crate::store::open_browser_data().
use crate::store;

/// Supprime toutes les entrées d'historique (avec sauvegarde silencieuse).
pub fn clear_all() -> Result<usize, String> {
    let s = store::open_browser_data()?;
    s.clear_history()
}

/// Recherche dans l'historique — retourne les entrées dont l'URL ou le titre contient `query`.
pub fn search(query: &str, limit: usize) -> Result<Vec<crate::browser_data::HistoryEntry>, String> {
    let s = store::open_browser_data()?;
    let all = s.recent_history(10_000)?;
    let q = query.to_lowercase();
    let results: Vec<_> = all
        .into_iter()
        .filter(|e| e.url.to_lowercase().contains(&q) || e.title.to_lowercase().contains(&q))
        .take(limit)
        .collect();
    Ok(results)
}
