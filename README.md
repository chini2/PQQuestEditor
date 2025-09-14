# PQQuest Save Editor — Antibombas (WinForms, .NET 6/8)

[![.NET](https://img.shields.io/badge/.NET-6%2F8-512BD4)](#)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D6)](#)
[![WinForms](https://img.shields.io/badge/GUI-WinForms-2EA043)](#)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Editor visual para **Pokémon Quest** que opera sobre el JSON exportado con `PqSave`.
Incluye un modo **antibombas** para evitar crashes corrigiendo valores fuera de rango y referencias inválidas.

> ⚠️ Haz _backup_ de tu partida antes de editar.

---

## Highlights

- UI sencilla: pestañas **Pokémon / Inventario / Misc** + log.
- **Validar / Auto‑arreglar**: niveles, stats, slots, `attach*`, `languageId`, IDs únicos.
- Cocina **separada** del inventario: edita `recipeID/rarityID/rankID/cookTime` en `cookingPotList[0]` (no toca cantidades).
- Layout robusto (SplitContainer + TableLayoutPanel) y grids con encabezados visibles.

---

## Requisitos

- Windows 10/11
- .NET **8** SDK (o .NET 9) con workloads de **Windows Desktop**

---

## Uso

1) **Abrir** `user.json` (exportado con `PqSave`).  
2) **Pokémon:** edita `Nivel / HP / ATK / Naturaleza (ID) / Slots`. (*El nombre solo se muestra.*)  
3) **Inventario:** cantidades por **ID** (mapea a nombre amigable).  
4) **Misc:** `Idioma`, `Tickets`.  
5) **Cocina (top bar):** `Receta / Rareza / Olla` → `visitCharacter.cookingPotList[0]`.  
6) **VALIDAR / AUTO‑ARREGLAR** → detecta y corrige valores inseguros.  
7) **Guardar** JSON y reempaca con `PqSave`.

---

## Rutas JSON (referencia rápida)

| Sección     | Ruta                                                                 |
|-------------|----------------------------------------------------------------------|
| Pokémon     | `SerializeData.characterStorage.characterDataDictionary`             |
| Inventario  | `SerializeData.itemStorage.datas`                                    |
| Cocina      | `SerializeData.visitCharacter.cookingPotList[0]`                     |
| Tickets     | `SerializeData.misc.fsGiftTicketNum` (`root.tickets` si no existe)   |
| Idioma      | `root.languageId`                                                    |

---

## Antibombas (qué valida/fija)

- IDs únicos en `characterDataDictionary`  
- Rangos: `level 1..100`, `hp/attack 0..9999`, `activeSlots 0..12`  
- Índices de `attachStoneStorageID` / `attachSkillStoneStorageID` (fuera de rango → `-1`)  
- `recipeID 0..17`, `rarityID 0..3`, `rankID 0..3` (no toca ingredientes)  
- `languageId` dentro del set conocido

---

## Licencia
PRODUCTO GRATUITO CHILENO
CON CARIÑO PARA LA COMUNIDAD DE POKEMON.
