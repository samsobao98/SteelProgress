# SteelProgress — Diseño: personalización total, entrenadores y compartir rutinas

| Campo | Valor |
|---|---|
| Estado | **Borrador para revisión** — no hay código implementado |
| Versión | 0.1 |
| Fecha | 2026-10-27 |
| Rama base | `maui` |
| Plataforma prioritaria | **Móvil (MAUI, Android primero)**; escritorio después reutilizando `SteelProgress.Core` |

---

## 1. Visión

SteelProgress deja de ser "una app para apuntar tu rutina" y pasa a ser una herramienta con **dos usos en la misma app**:

* **Deportista:** personaliza al máximo sus entrenamientos, crea sus propios ejercicios (con su propio vídeo) y registra su progreso.
* **Entrenador:** gestiona a sus clientes, les prepara rutinas dentro de la app, se las envía y recibe de vuelta lo que han entrenado.

Lo que la diferencia de otras apps:

1. **Circuito cerrado sin servidor.** El entrenador envía la rutina como archivo; el cliente entrena y devuelve sus sesiones como archivo; el entrenador las importa en la ficha del cliente y ve su progreso. Sin cuentas, sin suscripción, sin depender de internet.
2. **Ejercicios con vídeo propio.** Cada ejercicio puede tener el vídeo, GIF o imagen que el usuario elija de su galería, y viaja con la rutina si se quiere.
3. **Una sola app con dos vistas.** La vista simple para entrenar sin ruido y la vista entrenador con todas las opciones. Mismos datos, distinta densidad.

---

## 2. Principios de diseño

1. **La vista simple quita complejidad, nunca información.** Los datos que tenga una rutina (RPE, tempo, notas…) se ven siempre. Lo que se oculta son opciones de edición y configuración.
2. **Cambiar de vista no pierde ni altera datos.** Es solo una forma de mostrar lo mismo.
3. **Editar en vista simple no rompe datos avanzados.** Si el cliente cambia el peso de un ejercicio con RPE, el RPE se conserva.
4. **Todo lo compartible se diseña como "paquete".** El mismo paquete que hoy viaja como archivo debe poder viajar mañana por un servidor sin rediseñarlo.
5. **Lógica en `SteelProgress.Core`, pantallas en cada plataforma.** Lo que se construya para móvil sirve después para escritorio.

---

## 3. Vista simple y vista entrenador

### 3.1. Dónde se elige

* **Cada perfil tiene su propia vista**, guardada en el perfil (no es global de la app).
* Se cambia desde **Ajustes** (pantalla nueva) y se puede proponer al crear el perfil: "¿Para qué vas a usar SteelProgress? Entrenar / Entrenar y preparar rutinas a otros".
* Valor por defecto: **simple**.

### 3.2. Qué muestra cada vista

| Área | Vista simple | Vista entrenador |
|---|---|---|
| **Ver datos de la rutina** (RPE, tempo, descanso, notas, superseries…) | ✅ Siempre que la rutina los tenga | ✅ |
| **Entrenar y registrar** (series, repeticiones, peso) | ✅ | ✅ |
| **Edición básica** (añadir/quitar ejercicios, series, reps, peso, reordenar) | ✅ | ✅ |
| **Edición avanzada** (rangos de reps, RPE/RIR, % de 1RM, tempo, descanso, superseries, series de calentamiento, notas por serie) | ❌ Activable por bloques en Ajustes | ✅ |
| **Ejercicios propios** | Básico: nombre, grupo muscular, vídeo | Completo: además etiquetas, material, instrucciones, notas técnicas |
| **Clientes** | ❌ | ✅ |
| **Exportar rutina con opciones** (con/sin vídeos, para un cliente) | Exportar básico | ✅ |
| **Importar rutina recibida y devolver sesiones** | ✅ | ✅ |
| **Importar sesiones de un cliente** | ❌ | ✅ |

### 3.3. Ajustes de la vista simple (por bloques)

En Ajustes → "Mostrar en vista simple", interruptores por bloque, no por campo:

| Bloque | Qué activa en la edición |
|---|---|
| Intensidad | RPE / RIR y % de 1RM |
| Tempo y descanso | Tempo por ejercicio, descanso entre series |
| Estructura avanzada | Superseries y circuitos, series de calentamiento |
| Rangos | Repeticiones en rango (8–12) en lugar de un número fijo |
| Notas detalladas | Notas por serie además de por ejercicio |

Si se activan todos los bloques, la vista simple queda igual que la de entrenador salvo Clientes.

---

## 4. Clientes

* **Los clientes cuelgan del perfil del entrenador**, no son perfiles de la pantalla de inicio. Así un entrenador con 20 clientes no llena el selector de perfiles.
* Los perfiles de `LauncherPage` siguen siendo **las personas que usan ese móvil**.
* Cada cliente tiene:
  * datos básicos: nombre, color/avatar, notas, objetivo;
  * **sus rutinas** (creadas por el entrenador para él);
  * **sus sesiones devueltas** (importadas desde sus archivos);
  * su progreso, con las mismas gráficas que `ProgressPage`, pero sobre sus sesiones.
* Acción rápida: **"Enviar rutina"** desde la ficha del cliente.

---

## 5. Modelo de datos

### 5.1. Identificadores estables (requisito para compartir)

Hoy todas las entidades usan `int Id` autonumérico, que cambia de un móvil a otro. Para importar y exportar sin duplicar ni confundir datos, las entidades compartibles necesitan un **`Guid Uid`** además del `Id`:

* `Exercise`, `Routine`, `RoutineDay`, `RoutineDayExercise`, `WorkoutSession`, `Client`.
* El `Id` sigue siendo la clave interna; el `Uid` es la identidad entre dispositivos.
* La migración rellena un `Uid` nuevo para los datos existentes.

### 5.2. Cambios en entidades existentes

**`UserProfile`**
* `ViewMode` (`Simple` / `Trainer`).
* `SimpleViewBlocks` (banderas de los bloques de §3.3).
* Alternativa: una entidad `ProfileSettings` 1:1 si los ajustes crecen.

**`Exercise`**
* `Uid`.
* `Equipment` (material), `Instructions` (texto), `Tags` (lista simple).
* `Media` (colección de `ExerciseMedia`, ver §5.3).
* `Origin`: propio / importado (y de quién), para saber si se puede editar o si se actualizará al recibir otra versión.

**`Routine`**
* `Uid`.
* `ClientId` (nullable): si la rutina es para un cliente del entrenador.
* `AuthorName`, `SourceUid`, `Version`: quién la creó, de qué rutina original viene y su versión. Permite que el cliente sepa que es de su entrenador y que, al recibir la versión 2, la app ofrezca actualizar.

**`RoutineDayExercise`** — la prescripción. Hoy solo tiene `TargetSets` y `TargetReps`. Se añade:
* `RepsMin` / `RepsMax` (rango; si son iguales, reps fijas).
* `TargetWeight` o `TargetPercent1RM`.
* `TargetRpe` / `TargetRir`.
* `Tempo` (texto, p. ej. "3-1-1-0").
* `RestSeconds`.
* `WarmupSets`.
* `GroupLabel` (superseries: "A1", "A2"… mismo grupo = se hacen seguidos).
* `Notes`.
* Opcional, más adelante: **series prescritas una a una** (`PrescribedSet`) para pirámides o series distintas. Se empieza con prescripción por ejercicio, que cubre la mayoría de casos.

**`WorkoutSession`**
* `Uid`.
* `ClientId` (nullable): sesiones de un cliente importadas por el entrenador.
* `SourceRoutineUid`: de qué rutina venía.
* `Notes`, `DurationMinutes`.

**`WorkoutSet`**
* `Rpe` (registrado), `IsWarmup`, `Notes`.

### 5.3. Entidades nuevas

**`ExerciseMedia`**
* `Id`, `ExerciseId`, `Kind` (`Video` / `Gif` / `Image`), `FileName` (ruta relativa dentro del almacenamiento de la app), `Order`, `Caption`.

**`Client`**
* `Id`, `Uid`, `TrainerProfileId` (el `UserProfile` del entrenador), `Name`, `AvatarColor`, `Goal`, `Notes`, `CreatedAt`.
* Filtro por perfil actual, igual que `Routine` y `WorkoutSession` en `AppDbContext`.

### 5.4. Notas técnicas

* `AppDbContext` ya filtra por `CurrentUserId`; `Client` sigue el mismo patrón.
* `SaveChangesAsync` asigna `UserId` a ejercicios nuevos: los ejercicios importados deben entrar con el `UserId` del perfil que importa.
* `SteelProgress.Data` lo comparten WPF (net8) y MAUI (EF 10). Las migraciones nuevas tienen que funcionar en los dos o generarse con cuidado (ya hay un aviso suprimido por esta diferencia en `MauiProgram.cs`).

---

## 6. El paquete `.steel`

### 6.1. Formato

Un archivo `.steel` es un **ZIP** con:

```
manifest.json      tipo de paquete, versión del formato, autor, fecha, si incluye vídeos
data.json          rutinas / ejercicios / sesiones en JSON, con sus Uid
media/             vídeos e imágenes (solo si se exportó "con vídeos")
```

Tipos de paquete:

| Tipo | Quién lo genera | Contenido |
|---|---|---|
| `routine` | Entrenador o deportista | Una o varias rutinas con sus días, ejercicios y prescripción, más los ejercicios que usan |
| `exercises` | Cualquiera | Ejercicios sueltos (para compartir una biblioteca) |
| `sessions` | Cliente | Sesiones entrenadas de una o varias rutinas recibidas, para devolver al entrenador |

* `formatVersion` en el manifiesto desde el primer día, para poder cambiar el formato sin romper archivos antiguos.
* Al exportar se elige **con o sin vídeos**. Sin vídeos, el paquete lleva solo los datos; el ejercicio se importa sin multimedia (o conserva la que ya tuviera si existe).

### 6.2. Reglas de importación

* **Vista previa antes de importar:** qué contiene, de quién viene, cuántos ejercicios nuevos crea y si incluye vídeos.
* **Ejercicios:**
  * mismo `Uid` ya existente → se reutiliza; si el paquete trae versión más reciente, se ofrece actualizar;
  * `Uid` desconocido → se crea como ejercicio importado.
* **Rutinas:**
  * `SourceUid` ya existente con versión menor → "Tu entrenador ha actualizado esta rutina: ¿actualizar o guardar como copia?". El historial de sesiones no se toca.
  * nueva → se crea.
* **Sesiones (`sessions`) en el móvil del entrenador:** se elige a qué cliente pertenecen (o se reconoce si la rutina era de un cliente) y se guardan con su `ClientId`. Un `Uid` repetido no se duplica.

### 6.3. Flujos

```
ENTRENADOR                                  CLIENTE
Ficha del cliente → Crear/editar rutina
  → Enviar rutina (con/sin vídeos)
  → menú compartir de Android ─────────────► WhatsApp / correo / Drive
                                              → abre el .steel → vista previa → importar
                                              → entrena con la rutina
                                              → Historial → "Enviar a mi entrenador"
  ◄────────────────────────────────────────── .steel de tipo sessions
Abre el archivo → elige cliente → importar
  → progreso del cliente actualizado
```

El deportista que va por libre usa lo mismo para hacer copias de seguridad o compartir rutinas con amigos.

---

## 7. Multimedia de los ejercicios

* **Origen:** galería del móvil (`MediaPicker`), y opcionalmente grabar con la cámara.
* **Formatos aceptados:** vídeo (mp4, mov), GIF, imagen (jpg, png, webp).
* **Almacenamiento:** se copia al almacenamiento privado de la app (`FileSystem.AppDataDirectory/media/`). La base de datos guarda la ruta relativa, nunca la ruta de la galería, que puede desaparecer.
* **Reproducción:**
  * GIF: el control `Image` de MAUI los anima.
  * Vídeo: necesita un reproductor; la opción estándar es `MediaElement` de **CommunityToolkit.Maui** (dependencia nueva, a aprobar).
* **Tamaño:** límite configurable por vídeo (p. ej. 50 MB) y aviso del tamaño total al exportar con vídeos. La compresión de vídeo queda fuera de la primera versión.

---

## 8. Integración con Android

* **Exportar:** se genera el `.steel` en caché y se abre el menú compartir del sistema (`Share.RequestAsync` con `ShareFile`).
* **Abrir un `.steel`:** filtro de intents en `MainActivity` para abrir archivos `.steel` (y `application/zip` como respaldo, porque algunas apps cambian el tipo). La app recibe el archivo y muestra la vista previa de importación.
* **Elegir archivo desde la app:** botón "Importar" con `FilePicker`, para cuando el sistema no asocie la extensión.
* iOS y escritorio: después, con la misma lógica de `Core`.

---

## 9. Pantallas

| Pantalla | Cambio |
|---|---|
| `LauncherPage` | Al crear perfil, pregunta opcional "¿Para qué vas a usarla?" |
| `HomePage` | Acceso a Ajustes. En vista entrenador, resumen de clientes (últimas sesiones recibidas) |
| `ExercisePage` | Miniatura del vídeo en cada ejercicio; ficha de ejercicio con multimedia; crear/editar con "Añadir vídeo desde galería" |
| `RoutinePage` | Prescripción por ejercicio según la vista y los bloques activos; superseries; botón Exportar / Enviar |
| `WorkoutPage` | Muestra la prescripción (reps objetivo, RPE, descanso, notas del entrenador), botón para ver el vídeo del ejercicio, temporizador de descanso si hay `RestSeconds` |
| `HistoryPage` | "Enviar a mi entrenador" (exporta sesiones) |
| **`ClientsPage`** (nueva) | Lista de clientes y ficha de cliente: rutinas, sesiones recibidas, progreso. Solo en vista entrenador |
| **`SettingsPage`** (nueva) | Vista simple / entrenador, bloques de la vista simple, límites de vídeo |
| **`ImportPreviewPage`** (nueva) | Vista previa y confirmación de cualquier `.steel` |

La barra de pestañas tiene hoy 5 pestañas (Inicio, Ejercicios, Rutinas, Entreno, Historial). **Clientes** sería una sexta solo en vista entrenador, o un acceso desde Inicio para no saturar la barra (a decidir). **Ajustes** va desde Inicio, no en la barra.

---

## 10. Fases

Cada fase es usable por sí sola.

| Fase | Contenido | Resultado |
|---|---|---|
| **1. Base de datos preparada** | `Uid` en las entidades, campos nuevos de prescripción y registro, `ViewMode` en el perfil. Migración que conserva los datos actuales | Nada cambia a la vista; todo listo para lo demás |
| **2. Ajustes y vistas** | `SettingsPage`, vista simple/entrenador, bloques. Prescripción avanzada en `RoutinePage` y su visualización en `WorkoutPage` | Personalización completa de rutinas |
| **3. Multimedia** | `ExerciseMedia`, importar desde galería, ver vídeo/GIF en la ficha y durante el entreno | Ejercicios propios con vídeo |
| **4. Exportar e importar rutinas** | Paquete `.steel` tipo `routine` y `exercises`, con/sin vídeos, menú compartir, abrir archivo, vista previa | Se pueden pasar rutinas entre móviles |
| **5. Clientes** | `Client`, `ClientsPage`, rutinas por cliente, "Enviar rutina" | Entrenador organizado por clientes |
| **6. Devolución de sesiones** | Paquete `sessions`, "Enviar a mi entrenador", importar en la ficha del cliente, progreso del cliente | Circuito completo entrenador ↔ cliente |
| 7. Versiones de rutinas | Actualizar una rutina recibida sin perder el historial | Seguimiento a largo plazo |
| Después | Escritorio con las mismas funciones; envío dentro de la app con servidor; series prescritas una a una | — |

Recomendación: **1 → 2 → 3 → 4** primero; con eso la app ya se diferencia aunque no haya clientes. Las fases 5–6 completan la parte de entrenadores.

---

## 11. Decisiones pendientes

| # | Decisión | Opciones | Recomendación |
|---|---|---|---|
| D1 | Reproductor de vídeo | `CommunityToolkit.Maui.MediaElement` · Abrir el vídeo con el reproductor del sistema | `MediaElement`: el vídeo se ve dentro de la ficha y durante el entreno |
| D2 | Pestaña Clientes | Sexta pestaña en vista entrenador · Acceso desde Inicio | Pestaña: es la pantalla principal del entrenador |
| D3 | Prescripción | Por ejercicio · Serie a serie | Por ejercicio en la fase 2; serie a serie más adelante |
| D4 | Límite de tamaño de vídeo | 25 / 50 / 100 MB | 50 MB y aviso al exportar |
| D5 | Ejercicios importados | Editables · Solo lectura con opción de duplicar | Editables, marcando que vienen de otro autor |
| D6 | Biblioteca de ejercicios inicial | Vacía · Precargada con ejercicios comunes sin vídeo | Precargada: un usuario nuevo puede empezar sin crear nada |

---

## 12. Fuera de alcance de esta versión

* Cuentas, servidor y envío dentro de la app.
* Pagos o suscripciones para entrenadores.
* Compresión o edición de vídeo.
* Chat entre entrenador y cliente.
