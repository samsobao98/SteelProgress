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

### 4.1. Entrenar en vista entrenador: "¿Quién entrena?"

La pestaña Entreno se mantiene en la vista entrenador, convertida en **"Sesión con cliente"**:

* Muestra directamente la lista de clientes, con la rutina y el día que les toca. Elegir al cliente hace falta siempre, así que **no es un paso añadido**.
  * Solo se ofrecen las rutinas de ese cliente y la sesión se guarda **directamente en su ficha** (`ClientId`). Sirve para entrenamientos presenciales: el entrenador apunta la sesión desde su móvil y el cliente no tiene que exportar nada.
* Ajuste **"Yo también entreno con este perfil"**, **desactivado por defecto** en perfiles de entrenador:
  * desactivado: solo clientes; lo personal no aparece;
  * activado: **"Yo" aparece como primera opción de la misma lista** (sin pantalla extra) y el historial y el progreso propios se ven desde Inicio. Cubre a quien entrena y prepara rutinas a uno o dos amigos, o al entrenador que prueba una rutina antes de enviarla.
* **Cambiar de perfil tiene que ser rápido** (tocando el avatar en Inicio), para que usar dos perfiles no sea un engorro.
* Se pregunta al crear un perfil en modo entrenador. Las dos formas de trabajar son válidas:
  * **un solo perfil** para entrenar y llevar clientes (ajuste activado);
  * **dos perfiles**: uno de entrenador solo con clientes (ajuste desactivado) y otro personal en vista simple.
* Con dos perfiles, los ejercicios propios (y sus vídeos) son de cada perfil. Para no crearlos dos veces, hace falta **copiar ejercicios entre perfiles del mismo móvil**, reutilizando el paquete `exercises` sin pasar por un archivo.

### 4.2. Barra de pestañas según la vista

| Vista simple | Vista entrenador |
|---|---|
| Inicio · Ejercicios · Rutinas · Entreno · Progreso | Inicio · **Clientes** · Ejercicios · Rutinas · Entreno |

Progreso incluye el historial (§12.2). En vista entrenador, el historial y el progreso de cada cliente están en su ficha, y los propios (si el perfil también entrena) se abren desde Inicio.

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
* `Feeling` (1–5, "¿Cómo te has sentido?" de la pantalla de fin de entreno).
* `IsComplete` (falso si se guardó con series sin hacer).
* `IsInPerson`: sesión presencial registrada por el entrenador desde su móvil.

**`WorkoutSet`**
* `Rpe` (registrado), `IsWarmup`, `Notes`.

### 5.3. Entidades nuevas

**`ExerciseMedia`**
* `Id`, `ExerciseId`, `Kind` (`Video` / `Gif` / `Image`), `FileName` (ruta relativa dentro del almacenamiento de la app), `Order`, `Caption`.

**`Client`**
* `Id`, `Uid`, `TrainerProfileId` (el `UserProfile` del entrenador), `Name`, `AvatarColor`, `Goal`, `Notes`, `CreatedAt`.
* Filtro por perfil actual, igual que `Routine` y `WorkoutSession` en `AppDbContext`.

**`RoutineDelivery`** (lo que se ha enviado a cada cliente)
* `Id`, `ClientId`, `RoutineUid`, `Version`, `SentAt`, `Snapshot` (JSON de la rutina tal como se envió), `SentMediaUids` (vídeos que ya tiene el cliente).
* Permite detectar los cambios respecto a lo que tiene el cliente y enviar solo los vídeos nuevos (§6.4).

### 5.4. Notas técnicas

* `AppDbContext` ya filtra por `CurrentUserId`; `Client` sigue el mismo patrón.
* `SaveChangesAsync` asigna `UserId` a ejercicios nuevos: los ejercicios importados deben entrar con el `UserId` del perfil que importa.
* `SteelProgress.Data` lo comparten las pruebas (net8) y MAUI (EF 10); la app WPF ya no existe. Las migraciones nuevas tienen que funcionar en los dos o generarse con cuidado (ya hay un aviso suprimido por esta diferencia en `MauiProgram.cs`).

---

## 6. El paquete `.steel`

### 6.1. Formato

Un archivo `.steel` es un **ZIP** con:

```
manifest.json      tipo de paquete, versión del formato, autor, fecha, si incluye vídeos,
                   mensaje del entrenador y resumen de cambios (si es una actualización)
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

### 6.4. Versiones: detectar cambios y enviar solo lo nuevo

Aprobado por Samuel el 2026-10-27 (maquetas, bloque 6).

* **Detección de cambios.** Al enviar una rutina a un cliente, la app compara la rutina actual con el `Snapshot` de la última `RoutineDelivery` a ese cliente y genera un resumen legible:
  * ejercicios añadidos, quitados o sustituidos ("Pierna B cambia prensa por hack squat");
  * cambios de prescripción (series, repeticiones, RPE, descanso: "+1 serie en remo");
  * días añadidos, quitados o renombrados.
* Si hay cambios, la versión sube sola (v3 → v4) y el panel de envío muestra el resumen.
* **Mensaje del entrenador** opcional, que viaja en el manifiesto y el cliente ve al importar.
* **Solo vídeos nuevos.** Con `SentMediaUids` la app sabe qué vídeos tiene ya el cliente y ofrece enviar solo los nuevos o cambiados, mostrando el tamaño de cada opción.
* **Al importar (cliente):** la vista previa muestra "Actualización v3 → v4", los ejercicios y vídeos nuevos, el mensaje y "Tu historial se conserva". Opciones: actualizar o guardar como copia.
* **Al recibir sesiones (entrenador):** se reconoce al cliente por la `RoutineDelivery` de esa rutina; las sesiones ya importadas se marcan y no se duplican.

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

La barra de pestañas cambia según la vista (§4.2): en vista entrenador, **Clientes** ocupa el sitio de Historial. **Ajustes** va desde Inicio, no en la barra. `WorkoutPage` añade el paso "¿Quién entrena?" en vista entrenador (§4.1).

---

## 10. Fases

Cada fase es usable por sí sola.

| Fase | Contenido | Resultado |
|---|---|---|
| **1. Base de datos preparada** | `Uid` en las entidades, campos nuevos de prescripción y registro, `ViewMode` en el perfil. Migración que conserva los datos actuales | Nada cambia a la vista; todo listo para lo demás |
| **2. Ajustes y vistas** | `SettingsPage`, vista simple/entrenador, bloques. Prescripción avanzada en `RoutinePage` y su visualización en `WorkoutPage` | Personalización completa de rutinas |
| **3. Multimedia** | `ExerciseMedia`, importar desde galería, ver vídeo/GIF en la ficha y durante el entreno | Ejercicios propios con vídeo |
| **4. Exportar e importar rutinas** | Paquete `.steel` tipo `routine` y `exercises`, con/sin vídeos, menú compartir, abrir archivo, vista previa | Se pueden pasar rutinas entre móviles |
| **5. Clientes** | `Client`, `ClientsPage`, rutinas por cliente, "Enviar rutina", "¿Quién entrena?" y sesiones presenciales, copiar ejercicios entre perfiles | Entrenador organizado por clientes |
| **6. Devolución de sesiones** | Paquete `sessions`, "Enviar a mi entrenador", importar en la ficha del cliente, progreso del cliente | Circuito completo entrenador ↔ cliente |
| 7. Versiones de rutinas | Actualizar una rutina recibida sin perder el historial | Seguimiento a largo plazo |
| Después | Envío dentro de la app con servidor; series prescritas una a una. (La app MAUI ya corre en Windows con las mismas funciones) | — |

Recomendación: **1 → 2 → 3 → 4** primero; con eso la app ya se diferencia aunque no haya clientes. Las fases 5–6 completan la parte de entrenadores.

---

## 11. Decisiones pendientes

| # | Decisión | Opciones | Recomendación |
|---|---|---|---|
| ~~D1~~ | ~~Reproductor de vídeo~~ | **Decidido (2026-10-29):** `MediaElement` 7.0.0 en la ficha del ejercicio (Rediseño 4), con cámara lenta y sin sonido al empezar. En Entreno, de momento, sigue el del sistema | — |
| ~~D2~~ | ~~Pestaña Clientes~~ | **Decidido (2026-10-27):** pestaña en vista entrenador, en lugar de Historial; Entreno se mantiene con "¿Quién entrena?" y el ajuste "Yo también entreno" | — |
| D3 | Prescripción | Por ejercicio · Serie a serie | Por ejercicio en la fase 2; serie a serie más adelante |
| D4 | Límite de tamaño de vídeo | 25 / 50 / 100 MB | 50 MB y aviso al exportar |
| D5 | Ejercicios importados | Editables · Solo lectura con opción de duplicar | Editables, marcando que vienen de otro autor |
| ~~D6~~ | ~~Biblioteca de ejercicios inicial~~ | **Hecho:** precargada con 111 ejercicios comunes sin vídeo (`exercise_library.json`), con `Uid` fijo para que compartir rutinas no los duplique | — |

---

## 12. Guía visual

Maquetas aprobadas por Samuel el 2026-10-27 en `docs/maquetas/` (no son la app real; abrir los `.html` en el navegador, son interactivos):

* `propuesta_diseno.html` — propuesta de rediseño completa, con «antes» y «después»:
  0. Sistema de diseño · 1. Rutinas · 2. Entreno (registro, descanso, fin de entreno) · 3. Progreso · 4. Ejercicios (biblioteca, ficha, crear con vídeo) · 5. Inicio, elegir entreno y cambiar de perfil · 6. Entrenador (ficha de cliente, enviar, importar rutina, recibir sesiones).
* `vistas_y_clientes.html` — primeras maquetas: Ajustes (vista simple y bloques), entreno con rutina de un entrenador, Clientes y sesión con cliente.
* `img/` — capturas de las mismas.

### 12.1. Sistema de diseño

* **Tokens únicos** (colores, espaciado 4/8/12/16/24/32, radios 8/14/20, tipografía) definidos una vez en `Resources/Styles` y usados en todas las pantallas. Hoy los colores están escritos a mano en cada página y no coinciden (`#252536` en Inicio, `#313244` en Rutinas).
* **Color de marca: degradado acero** `#89B4FA → #B4BEFE → #CBA6F7`, solo para el botón principal, anillos de progreso y momentos importantes.
* **Colores con significado fijo:** hecho `#A6E3A1`, intensidad `#FAB387`, descanso `#94E2D5`, récord `#F9E2AF`, peligro `#F38BA8`.
* **Tipografía:** Space Grotesk (cifras de ancho fijo) para números y títulos; Open Sans para el texto. **Revisado (2026-10-29, estilo «Acero + Discos»):** Space Grotesk en todo, también el texto, y la paleta pasa a la de las maquetas nuevas (ver `IMPLEMENTACION.md`, Rediseño 5).
* **Placa biselada** para tarjetas protagonistas (rutina activa, «Hoy toca», serie actual).
* **Respuesta al tocar:** escala 0,96 con rebote (150 ms) en todo lo pulsable y vibración suave en acciones importantes. Animaciones de 150–300 ms que nunca frenan.

### 12.2. Decisiones de interfaz

* **Rutinas:** navegación en profundidad (rutina → días → ejercicios), crear con botón flotante y panel inferior, eliminar deslizando con deshacer, reordenar arrastrando. Sin formularios fijos.
* **Entreno:** números grandes con + / −, peso sugerido de la última vez, serie marcada con animación, insignia de récord, descanso en anillo, botón «Terminar» discreto y pantalla de fin con resumen, «¿Cómo te has sentido?», nota y aviso de series sin hacer.
* **Progreso** absorbe Historial: la barra de la vista simple queda en Inicio · Ejercicios · Rutinas · Entreno · Progreso; la de la vista entrenador, según §4.2.
* **Inicio:** tarjeta «Hoy toca» con el día siguiente de la rutina activa, tira de la semana, racha y récords; avatar para cambiar de perfil y engranaje para Ajustes.
* **Ejercicios:** miniatura del vídeo, filtros (incluido «Propios»), procedencia visible, ficha con vídeo a sangre, mejor marca, 1RM estimado y evolución.
* **Entreno libre** (sin rutina): propuesto en las maquetas, opcional y fuera de las fases actuales.

### 12.3. Orden recomendado del rediseño

Estado (2026-10-28): el punto 1 está hecho (`Tokens.xaml`, Space Grotesk); las pantallas aún no lo usan. Rutinas ya tiene la navegación en profundidad (lista → detalle con días y ejercicios reordenables) pero no el estilo de las maquetas.

1. Sistema de diseño (base para todo).
2. Entreno.
3. Rutinas.
4. Progreso (con Historial) e Inicio.
5. Respuesta táctil y animaciones en toda la app.

Encaja con las fases de §10: hacer el sistema de diseño antes de la fase 2 para que Ajustes, Clientes y las demás pantallas nuevas nazcan con el estilo final.

---

## 13. Fuera de alcance de esta versión

* Cuentas, servidor y envío dentro de la app.
* Pagos o suscripciones para entrenadores.
* Compresión o edición de vídeo.
* Chat entre entrenador y cliente.
