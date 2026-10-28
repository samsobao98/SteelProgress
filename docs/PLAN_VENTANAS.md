# Plan: ventanas y avisos con el diseño de la app

Los menús, confirmaciones, peticiones de texto y avisos de la app son los diálogos nativos del sistema (`DisplayActionSheetAsync`, `DisplayAlertAsync`, `DisplayPromptAsync`): no siguen el sistema de diseño y los avisos obligan a tocar «OK» incluso en mitad de un entreno. Este plan los sustituye por componentes propios.

## Inventario (2026-10-28)

| Tipo | Cuántas | Dónde |
|---|---|---|
| Avisos («Ejercicio añadido», «★ ¡Nuevo récord!», errores…) | 32 llamadas a `NotificationService`, un único punto que los muestra | `AppShell` los enseña como diálogo con «OK» |
| Confirmaciones | 9 en pantallas + 3 en la lógica (`ConfirmDialogService`) | Rutinas, detalle de rutina, Entreno, Clientes, selector de perfil |
| Menús de opciones | 8 | ⋮ de rutina, día, ejercicio y serie hecha; Ordenar; Compartir; Clientes; añadir vídeo |
| Pedir texto | 6 | Renombrar rutina o día, peso, reps, mensaje al compartir |
| Informativos con «OK» | 2 | «Sin día», «Permiso necesario» |

## Fase 0 — Componentes

Cuatro piezas con el mismo uso que las nativas, para que el cambio en cada pantalla sea mecánico:

* **Menú de opciones**: panel inferior oscuro, opciones en filas, la peligrosa en rojo, «Cancelar» abajo.
* **Confirmación**: tarjeta centrada; botón principal con el degradado acero, o rojo si es para eliminar.
* **Pedir texto**: tarjeta con un campo que se enfoca solo, teclado numérico cuando toca y el error dentro de la misma tarjeta.
* **Aviso breve**: mensaje abajo que desaparece solo; color según el tipo (correcto, error, récord) y botón de acción opcional («Deshacer»).

Las tres ventanas usan el `Popup` del CommunityToolkit (fondo oscurecido y botón Atrás de Android ya resueltos).

**Decisión tomada — aviso breve, opción A**: `Snackbar` del CommunityToolkit, con colores, fuente y esquinas del sistema de diseño (no admite iconos ni degradados). Si en el móvil no convence, se cambia por un aviso propio (opción B) sin tocar las llamadas: todo pasa por un único componente.

## Fase 1 — Avisos y confirmaciones centrales

* El punto único de `AppShell`: los 32 avisos pasan a aviso breve sin tocar las pantallas.
* Las 3 confirmaciones de la lógica (descartar entreno, terminar sin series, eliminar cliente) pasan a la confirmación propia.
* «Sin día» pasa a aviso breve.

## Fase 2 — Pantallas ya rediseñadas

* **Entreno**: borrar serie, escribir peso y reps.
* **Rutinas (lista)**: ⋮, Ordenar, renombrar, eliminar. El «Deshacer» actual pasa al aviso breve con acción.
* **Detalle de rutina**: menús de día y de ejercicio, renombrar día y las tres confirmaciones de eliminar.
* **Compartir**: con o sin vídeos y el mensaje.

## Fase 3 — Resto

Ejercicios (añadir vídeo, permiso), Clientes (menú) y selector de perfil (eliminar perfil).

## Fase 4 — Pruebas en el móvil

* El teclado no tapa la petición de texto.
* Atrás cierra cualquier ventana; tocar fuera cierra el menú.
* Un doble toque no abre dos ventanas.
* Windows: todo se puede usar con ratón.
* TalkBack lee el título y los botones.
* El aviso breve (Snackbar) no tapa la barra de pestañas, «Serie hecha» ni el botón +. Si lo hace, `Snackbar.Make` admite un elemento de anclaje.
* Rutinas: eliminar desde ⋮ y pulsar «Deshacer»; eliminar, cambiar de pestaña y volver (el aviso debe haberse ido y la rutina estar borrada).
* Abrir una ventana con la vista previa de una rutina abierta.

## Estado

| Fase | Estado |
|---|---|
| 0 — Componentes | Hecho: `Services/AppDialogs.cs` (menú, confirmación, aviso, petición de texto) y `Services/AppToast.cs` |
| 1 — Avisos y confirmaciones centrales | Hecho: `AppShell` conecta `NotificationService` y `ConfirmDialogService`; «Sin día» es un aviso breve; el récord sale en dorado |
| 2 — Pantallas rediseñadas | Hecho: Entreno, Rutinas (el «Deshacer» es ahora el aviso breve con acción), detalle y Compartir |
| 3 — Resto | Hecho: Ejercicios, Clientes y selector de perfil |
| 4 — Pruebas en el móvil | Pendiente |

Ya no queda ningún `DisplayActionSheetAsync`, `DisplayAlertAsync` ni `DisplayPromptAsync` en la app.

### Detalles de la implementación

* Una sola ventana a la vez: si ya hay una abierta, la siguiente no se abre (un doble toque no duplica).
* Tocar fuera o Atrás equivale a «Cancelar».
* La petición de texto valida dentro de la misma ventana (p. ej. el peso en Entreno: «Un número entre 0 y 1000»).
* `ConfirmDialogService` (Core) admite ahora el texto del botón y si es destructivo (`ConfirmRequest`).
* **Windows**: el Snackbar necesitaría registrar la app para notificaciones del sistema, así que allí el aviso breve es una tarjeta propia abajo que se cierra sola. También se usa si el Snackbar fallara en el móvil. Mientras está visible bloquea los toques unos segundos; tocar fuera la cierra y ese toque se pierde.
* **Las ventanas son páginas modales** (así funciona el `Popup` del Toolkit): al abrir una, la pantalla de debajo recibe `OnDisappearing` y al cerrarla `OnAppearing`. Todas las pantallas lo ignoran cuando la causa es una ventana propia (`AppDialogs.IsShowing`); si no, Rutinas confirmaría el borrado pendiente al abrir el ⋮ y las listas se recargarían con cada aviso.
