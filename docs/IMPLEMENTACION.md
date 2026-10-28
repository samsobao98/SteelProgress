# Implementación por fases — estado y cómo probar

> **Estado (2026-10-28):** las cinco fases están fusionadas en `maui` junto con el trabajo local que no estaba en la nube (ver [Fusión con `maui`](#fusión-con-maui-2026-10-28)). Probadas por el usuario tras la fusión. Las ramas `fase*` se conservan solo como referencia.

Cada fase se desarrolló en **su propia rama**, encadenada a la anterior:

```
maui ── diseno-entrenador ── fase1-base-datos ── fase2-ajustes ── fase3-multimedia ── fase4-compartir ── fase5-clientes
        (diseño y maquetas)   (BD + tokens)       (Ajustes,        (vídeos e           (exportar e         (clientes del
                                                   vistas,          imágenes)           importar .steel)    entrenador)
                                                   prescripción)
```

Para probar todo: compilar y ejecutar `maui` (Windows Machine o Android desde Visual Studio).

## Cómo se ha verificado (sin Android)

En la nube no hay SDK de Android, así que:

* **Pruebas automáticas** (`SteelProgress.Tests`, 85): migraciones sobre una base de datos con datos antiguos, lógica de vistas, ajustes, prescripción y multimedia. Se ejecutan con `dotnet test SteelProgress.Tests`.
* **Compilación de la app móvil** como `net10.0` (sin Android) para validar todo el C# y el XAML de `SteelProgress.Maui`. Detectó y corrigió claves de recursos duplicadas que habrían roto la compilación.
* **Enlaces de datos** de las pantallas nuevas comprobados con bindings tipados.
* **Recursos** (`StaticResource`) de todas las pantallas comprobados con un script: un recurso que falta no lo detecta el compilador y haría fallar la pantalla al abrirse.
* **Código de Android** (`MainActivity`) compilado contra las librerías de referencia de Android, con MAUI simulado.

Lo que **no** se ha podido comprobar en la nube: cómo se ve, el comportamiento táctil, la galería y la cámara del móvil, y la compilación real para Android/iOS.

Tras la fusión, en el PC: 88 pruebas automáticas en verde, compilación real para Android y Windows sin errores ni avisos, migración comprobada sobre una copia de la base de datos real y prueba manual de la app.

---

## Fase 1 — `fase1-base-datos`

**Qué hace**
* Migración `AddSharingAndPrescription`: identificador único (`Uid`) en ejercicios, rutinas, días, ejercicios de día y sesiones (se rellena solo para los datos existentes); prescripción ampliada (rango de reps, peso/%1RM, RPE/RIR, tempo, descanso, calentamiento, superserie, notas); sesión con duración, sensación, notas y si quedó completa; serie con RPE, calentamiento y nota; perfil con vista simple/entrenador, bloques y «Yo también entreno».
* Sistema de diseño: `Resources/Styles/Tokens.xaml` (colores, degradado acero, radios, espaciado, estilos de texto, tarjetas, chips y botones) y fuente Space Grotesk. No cambia ninguna pantalla por sí solo.

**Probar**
1. Abrir la app con tus datos actuales: debe actualizar la base de datos al arrancar y mostrar todo igual que antes.

## Fase 2 — `fase2-ajustes`

**Qué hace**
* **Ajustes** (engranaje en Inicio): vista simple o entrenador, bloques de la vista simple y «Yo también entreno». Se guarda al momento. Primera pantalla con el sistema de diseño.
* **Detalle de rutina**: cada ejercicio muestra lo prescrito («4 × 6–8 · RPE 8 · 1:30») y el menú ⋮ → «Editar series, reps e intensidad» abre un panel inferior. Los campos avanzados aparecen según la vista y los bloques, o si ya tienen valor (la vista simple nunca esconde datos).
* **Entreno**: debajo del ejercicio seleccionado, el objetivo y la nota del entrenador.

**Probar**
1. Inicio → engranaje → cambiar entre Simple y Entrenador; activar y desactivar bloques; salir y volver: debe recordarse.
2. Rutinas → abrir una rutina → elegir el día → ⋮ del ejercicio → «Editar series…». En vista simple solo series, reps y notas; activar «Intensidad» en Ajustes y volver: aparece la sección. En vista entrenador, todo.
3. Escribir valores (probar «72,5» con coma) y guardar; ver el texto bajo el ejercicio. Probar un valor incorrecto (RPE 11): debe avisar sin guardar.
4. Iniciar un entreno de ese día: debe verse «Objetivo: …» y la nota.

## Fase 3 — `fase3-multimedia`

**Qué hace**
* Migración `AddExerciseMedia` y almacenamiento en la carpeta privada de la app (`AppDataDirectory/media`), con nombres generados, solo formatos admitidos y máximo de 50 MB.
* **Ejercicios**: en el formulario, sección «Vídeos e imágenes» para elegir de la galería (varios a la vez), grabar un vídeo si el móvil lo permite y quitar. Miniatura en la lista. Al cancelar un ejercicio nuevo se borran los archivos elegidos; al borrar un ejercicio, también.
* **Entreno**: botón «▶ Ver cómo se hace» si el ejercicio tiene vídeo o imagen.
* El vídeo se abre con el reproductor del móvil. El reproductor dentro de la app depende de la decisión **D1** (añadir `CommunityToolkit.Maui.MediaElement`), pendiente de tu aprobación.
* Permisos de Android (cámara, micrófono, almacenamiento en Android ≤ 12) y textos de privacidad de iOS.

**Probar**
1. Ejercicios → «+» → «Añadir vídeo o imagen» → elegir un vídeo, un GIF y una foto → guardar. Ver la miniatura en la lista.
2. Tocar la miniatura del vídeo en el formulario: debe abrirse el reproductor.
3. Quitar uno con la ✕ y volver a entrar: no debe estar.
4. Crear un ejercicio, añadir un vídeo y cerrar sin guardar: no debe quedar nada.
5. Un vídeo de más de 50 MB: debe avisar.
6. En un entreno con ese ejercicio: «▶ Ver cómo se hace».

## Fase 4 — `fase4-compartir`

**Qué hace**
* Archivo **`.steel`**: un ZIP con `manifest.json` (autor, mensaje, fecha, si lleva vídeos), `data.json` (ejercicios y rutinas con su prescripción) y `media/` si se exporta con vídeos.
* **Compartir** (icono arriba a la derecha en el detalle de la rutina): elegir con o sin vídeos (se ve el tamaño), escribir un mensaje opcional y compartir con el menú del móvil (WhatsApp, correo, Drive…).
* **Importar** (icono de descarga junto al título de Rutinas): elegir un `.steel`; se abre la **vista previa** con autor, mensaje, días, ejercicios nuevos, vídeos y, si ya la tienes, «Actualización vX → vY».
* **Importar** reutiliza los ejercicios que ya tienes (por su `Uid`, también los de la biblioteca precargada), solo trae los vídeos que faltan y:
  * **Actualizar rutina**: cambia la rutina que ya tienes sin perder historial (los días se reutilizan por orden y un día con entrenos nunca se borra);
  * **Guardar como copia**: crea otra rutina («Nombre (v4)»).
* Si un ejercicio importado se llama igual que uno tuyo distinto, entra como «Nombre (2)».
* Si algo falla a mitad, no queda nada a medias (ni datos ni archivos).
* Migración `MakeUidsUniquePerOwner`: el `Uid` de ejercicios y rutinas pasa a ser único por perfil, para que dos perfiles del mismo móvil puedan importar el mismo archivo.
* **Abrir desde otras apps (Android)**: tocar un `.steel` en WhatsApp o en el correo y elegir SteelProgress. Si aún no has elegido perfil, se abre al elegirlo. Va en su propio commit (`ce629f9`): si diera problemas al compilar o abrir, se puede revertir solo ese.

**Probar**
1. Rutinas → abrir una rutina → icono de compartir → con vídeos → escribir un mensaje → enviártelo (por ejemplo, a ti mismo por WhatsApp o a Drive).
2. En el mismo móvil, Rutinas → icono de importar → elegir el archivo: debe decir que ya la tienes («Actualización v1 → v1») y ofrecer guardarla como copia.
3. Con otro perfil (o en otro móvil): importar → debe crear la rutina con sus ejercicios, prescripción y vídeos.
4. Cambiar algo de la rutina original, subir su versión (de momento no hay botón: se hará en la fase 7) y volver a compartir para probar «Actualizar».
5. Tocar el `.steel` desde WhatsApp o un gestor de archivos → «Abrir con SteelProgress».
6. Un archivo que no sea `.steel`: debe avisar.

## Fase 5 — `fase5-clientes`

**Qué hace**
* Migración `AddClients`: tabla de clientes (cuelgan del perfil del entrenador), y rutinas y sesiones con `ClientId`; las sesiones registradas por el entrenador con el cliente delante quedan marcadas como **presenciales**.
* **Cliente activo**: en lugar de duplicar pantallas, el entrenador «entra» en un cliente y **Inicio, Rutinas, Entreno y Progreso** (antes Historial) pasan a mostrar y guardar los datos de ese cliente. Un aviso «Trabajando con Marta · Salir» lo indica y, al salir, todo vuelve a tus datos. Durante un entreno no se puede salir.
* Tus estadísticas de Inicio no cuentan las sesiones de clientes (con un cliente activo, Inicio muestra las suyas).
* **Pestaña Clientes** (solo en vista entrenador): lista con objetivo, rutina, sesiones de la semana y aviso en rojo si lleva 7 días o más sin entrenar. Al tocar uno: entrenar con él, ver sus rutinas, ver su historial, editar o eliminar (borra sus rutinas y sesiones, no las tuyas).
* En vista entrenador, **Progreso** (antes Historial) solo aparece si el perfil «también entrena» (Ajustes) o mientras se trabaja con un cliente.
* **Importar** con un cliente activo crea la rutina para ese cliente.

**Probar**
1. Ajustes → Entrenador: aparece la pestaña Clientes (y desaparece Historial si «Yo también entreno» está apagado).
2. Clientes → «+ Cliente» → nombre, objetivo, color → Guardar.
3. Tocar el cliente → «Rutinas de…»: la lista sale vacía y con el aviso arriba. Crear una rutina para él. «Salir»: vuelven tus rutinas.
4. Tocar el cliente → «Entrenar con…» → hacer un entreno. Ver «Historial de…» y comprobar que no aparece en tu historial ni en tus números de Inicio.
5. Volver a Clientes: el cliente muestra «1 esta semana».
6. Cambiar de perfil con un cliente activo: debe salir del cliente.

**Aún no está (para siguientes pasos)**
* «Enviar rutina» directo desde la ficha del cliente (hoy: entrar en el cliente → Rutinas → Compartir).
* Ficha de cliente completa de la maqueta (pestañas Sesiones/Rutinas/Progreso/Notas, adherencia, récords).
* Copiar ejercicios entre perfiles del mismo móvil.
* En vista entrenador sin «Yo también entreno», la pestaña Entreno aún permite entrenos propios si no hay cliente activo.
* Fase 6: devolver sesiones al entrenador (paquete `sessions`) y fase 7: detección automática de cambios y versión al reenviar.

---

## Fusión con `maui` (2026-10-28)

Mientras se hacían las fases en la nube, en `maui` (PC) había 7 commits que no estaban subidos:

* **Rutinas rediseñadas**: lista de rutinas con resumen y una pantalla de detalle con días en chips y ejercicios reordenables arrastrando (`RoutineDetailPage`).
* **Biblioteca de ejercicios precargada** (decisión D6): 111 ejercicios de sistema (`IsSystem`) desde `Resources/Raw/exercise_library.json`, con músculos secundarios, material e instrucciones; ficha de detalle en Ejercicios y distintivo «Propio» en los del usuario.
* **La app MAUI corre también en Windows** (MSIX); la app WPF (`SteelProgress.App`) queda retirada de la solución.
* Arreglos de `LauncherPage` y del botón flotante, y `SQLitePCLRaw` 2.1.13 (CVE-2025-6965).

Cómo se resolvieron los conflictos:

* **Rutinas**: se mantiene lista + detalle. Lo de la nube pasa a esas pantallas: editor de prescripción y Compartir en el detalle; Importar y el aviso de cliente activo en la lista.
* **Ejercicios**: la ficha de detalle y la biblioteca conviven con las miniaturas y la sección de vídeos e imágenes.
* **Migraciones**: `AddExerciseLibraryFields` (local, 2026-07-14) y `AddSharingAndPrescription` añadían las dos `Equipment` e `Instructions`. Se quitaron de la segunda y el snapshot recoge `SecondaryMuscles` e `IsSystem`. Un móvil que hubiera instalado la versión de la nube antes de la fusión fallaría al migrar: habría que borrar los datos de la app (no se llegó a instalar en ninguno).
* **Biblioteca y compartir**: los ejercicios de sistema tienen un `Uid` fijo calculado a partir del nombre, igual en todos los móviles, para que una rutina importada los reconozca en vez de duplicarlos. Al arrancar se corrigen los de bibliotecas ya sembradas.
* **Pruebas**: nueva `MigrationTests.ModelSnapshotMatchesCurrentModel`, que falla si el snapshot de EF no coincide con el modelo (por ejemplo, tras fusionar ramas con migraciones).

---

## Siguiente: rediseño de pantallas

El sistema de diseño (`Tokens.xaml`, Space Grotesk) ya existe, pero las pantallas antiguas siguen con colores escritos a mano. Orden acordado (§12.3 del documento de diseño), siguiendo las maquetas de `docs/maquetas/propuesta_diseno.html`:

1. Entreno — **hecho** (ver abajo).
2. Rutinas — **hecho** (ver abajo).
3. Progreso (absorbe Historial) e Inicio — **hecho, pendiente de probar en el móvil** (ver abajo).
4. Ejercicios (filtro «Propios», ficha con vídeo, mejor marca, 1RM estimado).
5. Respuesta táctil y animaciones en toda la app.

### Rediseño 1 — Entreno

**Qué hace**
* **Preparar** («¿Qué entrenas hoy?»): rutinas en chips, días en tarjetas con «Hace N días» y vista previa del día con lo prescrito. Se abre con la rutina activa (la última entrenada) y el día que toca, marcado con «Toca». Lo que elijas a mano se respeta mientras estés en la pantalla; al terminar un entreno vuelve a proponer el siguiente.
* **Registrando**: ejercicio N de M con barra de progreso (tocar un tramo cambia de ejercicio), cronómetro y «Terminar» discreto arriba. Chips de objetivo, intensidad y vídeo. Números grandes con − / + (2,5 kg y 1 rep por pulsación; tocar el número para escribirlo). Empieza con lo de la última vez (misma serie) o, si no hay, con lo prescrito.
* **Cada serie se guarda al marcarla** (antes solo al finalizar): si la app se cierra, las series no se pierden. Tocar una serie hecha permite borrarla.
* **Récords**, frente a todo tu historial de ese ejercicio y las series previas de hoy (sin historial no hay récord); vibra y muestra la insignia:
  * **★ Récord de peso**: más kilos que nunca, con cualquier número de repeticiones.
  * **★ Récord de reps**: más repeticiones que nunca con ese peso o más (también sirve para ejercicios sin peso).
  * No se usa el 1RM estimado: con fórmulas como Epley, bajar peso y hacer una rep más podía contar como récord y subir peso no siempre. El 1RM estimado queda para la gráfica de Progreso (`WorkoutRepository.EstimatedOneRepMax`).
* **Descanso**: anillo con cuenta atrás (lo prescrito o 90 s), −15 s / +15 s / Saltar, qué viene después y volumen de hoy frente a la última vez que hiciste ese día. Vibra al acabar.
* **Finalizar**: se abre al pulsar «Terminar» o solo al hacer la última serie. Duración, volumen, series, récords, «¿Cómo te has sentido?», nota y aviso de series sin hacer (se guarda como incompleto). Seguir entrenando o Descartar.
* El historial (sugerencias, récords, volumen) es solo del perfil y cliente activos.

**Probar**
1. Entreno → elegir rutina y día → ver la vista previa → Empezar.
2. Ajustar peso y reps con − / + y escribiendo; «Serie hecha»: vibra y empieza el descanso.
3. En el descanso: −15 / +15 / Saltar; dejar que acabe y comprobar que vibra.
4. Repetir el mismo día otro día: debe proponer los pesos de la última vez y marcar récord al superarlos.
5. «Terminar» a medias: aviso de series pendientes; guardar y ver la sesión en Historial.
6. Cambiar de pestaña con el descanso en marcha y volver: el tiempo debe seguir bien.

**Aún no está**
* Si la app se cierra a mitad de entreno, las series están guardadas pero la sesión no se retoma al volver a abrirla.
* «Entreno libre» (sin rutina) y la tarjeta «Hoy toca» (fase de Inicio).

### Rediseño 2 — Rutinas

**Qué hace**
* **Lista**:
  * La **rutina activa** va en una placa con su semana. Es la que marques con «Marcar como activa» dentro de la rutina («● ACTIVA · ELEGIDA»); si no marcas ninguna, la última que has entrenado. Cada cliente tiene la suya. Migración `AddRoutineMarkedActive`.
  * La placa muestra su semana: un tramo por día, «Esta semana: 2 de 4» y «Toca: Pierna B». El resto, en tarjetas con insignia («5×5», «FB») y «última vez hace 3 semanas».
  * **Orden**: automático (activa primero y el resto por última vez entrenada) o a mano con «Ordenar» → arrastrar → «Hecho». «Volver al orden automático» deshace el orden manual. Con orden manual, las rutinas nuevas o importadas van al final. Migración `AddRoutineSortOrder`.
  * **Eliminar deslizando** a la izquierda, con «Deshacer» durante 5 s. El borrado real ocurre al caducar el aviso o al salir de la pantalla.
  * **Tocar** una rutina entra en ella. **Mantenerla pulsada** abre su resumen sin entrar: días con su nº de ejercicios y cuándo se hicieron, entrenos totales y de este mes, duración media y el último entreno; abajo, «Entrar en la rutina».
  * **⋮** en cada tarjeta: Ver resumen, Cambiar nombre y notas, Marcar/Quitar como activa, Compartir y Eliminar (mismo aviso y Deshacer). Es la forma visible de llegar a todo y la que funciona en Windows; deslizar y mantener pulsado son atajos.
  * Mantener pulsado usa `TouchBehavior` de **CommunityToolkit.Maui 13.0.0** (MAUI 10.0.20 no lo trae). Es la última versión compatible con MAUI 10.0.20; las siguientes piden 10.0.41 o más. El mismo paquete sirve para la decisión D1 (vídeo dentro de la app).
* **Detalle**: título con notas y nº de días, días en chips (el activo con el degradado acero), ejercicios con su superserie (A1) o posición y lo prescrito. Los que no tienen series indicadas muestran «+ Añadir series y reps», porque en Entreno sin objetivo no se sabe cuándo está hecho.
* **Añadir ejercicios**: panel con buscador (sin distinguir tildes), filtro por grupo, miniatura y selección múltiple; se añaden en el orden en que se tocaron. Se puede repetir un ejercicio en el mismo día.
* **Ojo, comportamiento que ya existía**: borrar una rutina o un día borra también sus entrenos del historial. Ahora se avisa con el número de entrenos antes de borrar. Conservar el historial exigiría cambiar el esquema de la base de datos.
* «Toca» lo calcula `RoutineSchedule` (Core): el siguiente día al último entrenado que no se haya hecho esta semana (la semana empieza el lunes). Lo reutilizará Inicio para «Hoy toca».

**Probar**
1. Rutinas: la última entrenada sale arriba en la placa con su semana y el día que toca.
1. Entrar en otra rutina → «Marcar como activa» → volver: sale en la placa como «ACTIVA · ELEGIDA» y Entreno la propone. «Quitar» vuelve a la última entrenada.
2. «Ordenar» → «Ordenar a mano» → arrastrar → «Hecho»; salir y volver: se mantiene. Crear una rutina: va al final. «Volver al orden automático».
3. Deslizar una rutina a la izquierda → Eliminar (si tiene entrenos, avisa cuántos) → «Deshacer». Repetir sin deshacer y cambiar de pestaña: ya no está.
4. Entrar en una rutina → + → buscar «biceps», filtrar por grupo, marcar varios → «Añadir N ejercicios». Deben salir en ese orden y con «+ Añadir series y reps».
5. Mantener pulsada una rutina: vibra y sale el resumen; tocar fuera lo cierra; «Entrar en la rutina» entra. Tocar normal sigue entrando directamente.
6. ⋮ de una tarjeta: probar cada opción. Comprobar que tocar el ⋮ no entra en la rutina.
7. Con «Ordenar» activo, arrastrar debe seguir funcionando (mantener pulsado no abre el resumen en ese modo).
8. En Windows: deslizar y mantener pulsado con el ratón pueden no funcionar; el ⋮ sí.

**Aún no está**
* «Semana 3 de 8» de la maqueta: haría falta guardar la duración del programa.
* Tocar «Toca: …» en la placa no empieza el entreno: se hace desde Inicio o desde el resumen (mantener pulsado).

### Rediseño 3 — Inicio y Progreso

**Qué hace**
* **Inicio**:
  * **«Hoy toca»** sustituye al botón genérico. Muestra la rutina activa (la marcada o la última entrenada), el día que toca (`RoutineSchedule`), sus ejercicios, la duración media de ese día y cuándo se hizo por última vez.
  * Si hoy ya has entrenado, la tarjeta lo dice («✓ Hoy ya has entrenado Torso A») y pasa a «Siguiente».
  * **«▶ Empezar»** abre Entreno con la sesión ya empezada. «Elegir otro entreno» abre Entreno con el día propuesto. Con un entreno a medias, la tarjeta pasa a «Continuar entreno»; sin rutinas, invita a crear la primera.
  * Tira de la semana (L–D) con los días entrenados y hoy con contorno.
  * Sesiones, **semanas seguidas** 🔥 y **récords del mes** ★.
  * Últimas 3 sesiones con su duración y volumen; tocar una la abre en Progreso.
* **Progreso** sustituye a la pestaña Historial y tiene tres secciones:
  * **Resumen**: anillo del objetivo semanal con el degradado acero, racha, volumen del mes frente al mismo tramo del mes pasado, calendario de constancia de 15 semanas (más claro cuanto más volumen) y récords recientes.
  * **Ejercicios**: cada ejercicio entrenado con su mejor serie y su 1RM estimado. Al tocar uno sale la gráfica de peso máximo y 1RM estimado por sesión.
  * **Historial**: todas las sesiones con día, rutina, duración, volumen, cómo te sentiste y si quedó incompleta. Tocar una abre su detalle: series, comparación con la vez anterior de ese día, récords y nota. Desde ahí, «Evolución ›» lleva a la gráfica del ejercicio.
* **Resumen de rutina** (mantener pulsado en Rutinas): nuevo botón «▶ Empezar <día que toca>».
* Clientes → «Historial de…» abre Progreso → Historial de ese cliente.

**Definiciones**
* Solo cuentan las sesiones con alguna serie que no sea de calentamiento. Al empezar un entreno ya se crea su sesión vacía; antes, Inicio la contaba.
* **Récords**: la misma regla que Entreno, en una sola función (`TrainingStats.RecordOf`). Al repasar el historial, la primera sesión de cada ejercicio no marca récords. Por sesión y ejercicio cuenta uno (el de peso antes que el de reps).
* **Semanas seguidas**: semanas (de lunes a domingo) con algún entreno. Si esta semana aún no has entrenado, la racha se cuenta hasta la semana pasada, para que no se rompa el lunes.
* **Objetivo semanal**: los días de la rutina activa (3 si no hay). No hay un campo propio para elegirlo.
* **Volumen del mes**: kg × reps sin calentamiento. Se compara con el mes pasado del día 1 al mismo día de hoy.
* Lo renombrado en Rutinas se ve al momento en Inicio y en Entreno (antes Entreno podía seguir mostrando el nombre anterior hasta reiniciar la app).
* Todo sale de una sola carga del historial (`WorkoutRepository.GetTrainingLogAsync`) y de cálculos en `TrainingStats` (Core), con pruebas en `ProgressStatsTests`.

**Probar**
1. Inicio con una rutina activa: «Hoy toca» muestra el día siguiente al último que hiciste. «▶ Empezar» abre Entreno ya registrando ese día.
2. Con el entreno a medias, volver a Inicio: sale «Continuar entreno». Terminarlo: la tira marca hoy con ✓ y la sesión sale en «Últimas sesiones».
3. Tocar una sesión de Inicio: abre Progreso → Historial con su detalle. Atrás (o tocar fuera) lo cierra.
4. Progreso → Resumen: el anillo, la racha y el calendario cuadran con lo que has entrenado. Hacer un récord y comprobar que sale en «Récords recientes».
5. Progreso → Ejercicios → tocar uno: sale la gráfica; tocarlo otra vez la quita.
6. Rutinas → mantener pulsada una rutina → «▶ Empezar …».
7. Vista entrenador con «Yo también entreno» apagado: Progreso no aparece; Clientes → «Historial de…» lo muestra con los datos del cliente. Pulsar «Salir» estando en Progreso: vuelve a Clientes y la pestaña desaparece.
8. Cambiar rápido entre Inicio y Progreso varias veces: no debe dar error.

**Aún no está**
* Cambiar de perfil tocando el avatar: hoy no hay forma de volver al selector de perfiles desde dentro de la app y hay que reiniciar el estado de todas las pantallas. Queda como paso aparte.
* «Entreno libre» (sin rutina): necesita cambiar la base de datos (una sesión siempre pertenece a un día de rutina).
* Elegir el objetivo semanal a mano y «Ver todos» los récords.

### Notas por serie y ajustes que faltaban

**Qué hace**
* **Notas por serie**: en Entreno, tocar una serie hecha → «Añadir nota». Se ven bajo la serie, en Progreso → Historial (detalle de la sesión) y la próxima vez, debajo de «La última vez». Se puede apuntar en vista entrenador o con «Notas detalladas» en vista simple; una nota que ya existe se ve y se corrige siempre. Usa `WorkoutSet.Notes`, que ya existía: sin cambios en la base de datos.
* **Material e instrucciones** de los ejercicios propios: en el formulario de Ejercicios, solo en vista entrenador (si el ejercicio ya los tiene, se ven en cualquier vista). El formulario ahora se desplaza si no cabe.
* Texto de «Yo también entreno»: decía que mostraba «Yo» en Entreno, que no existe; lo que hace es mostrar Progreso sin cliente activo.

**Probar**
1. Vista simple sin «Notas detalladas»: tocar una serie hecha solo ofrece «Borrar serie».
2. Activar «Notas detalladas» (o vista entrenador) → tocar la serie → «Añadir nota» → guardar: aparece «📝 …» bajo la serie y en Progreso → Historial.
3. Siguiente entreno de ese día: la nota sale bajo «La última vez».
4. Vista entrenador → Ejercicios → nuevo ejercicio: campos Material e Instrucciones; guardar y abrir su ficha.

---

## Pendiente de decidir

* **D1** — reproductor de vídeo dentro de la app (`CommunityToolkit.Maui.MediaElement`, paquete aparte del Toolkit que ya usamos) o seguir con el del sistema.
* Límite de vídeo configurable en Ajustes (hoy fijo en 50 MB).

## Notas técnicas

* `Directory.Build.props` usa `C:\SPBuild\` (ruta de Windows). Para compilar en Linux se usa una copia temporal con otra ruta; no hace falta cambiar nada en tu PC.
* `ExerciseViewModel` y `RoutineDetailViewModel` aceptan sus servicios nuevos (multimedia, opciones de vista) como opcionales para poder crearlos a mano en las pruebas. La app WPF, que era el motivo original, ya no está en la solución.
* Corregido durante las pruebas: al quitar un vídeo de un ejercicio cargado, EF Core lo «resucitaba» en vez de borrarlo (`ExerciseRepository.RemoveMediaAsync`). Lo mismo se evita al quitar días en una actualización de rutina.
* Ojo: la asignación automática del perfil a los datos nuevos (`AppDbContext.SaveChangesAsync`) solo actúa con `SaveChangesAsync`, no con `SaveChanges`. El código nuevo usa siempre la versión asíncrona.
