# Cronograma · Londoño Gómez

Planificador de tareas para un equipo de dos personas. Tiene fases, tareas con subtareas, responsables, fechas de inicio y entrega, estados y prioridades, y cuatro vistas: **Lista**, **Tablero** (arrastrar y soltar), **Cronograma** (Gantt) y **Calendario**. Marca los fines de semana y los festivos de Colombia.

- **.NET 10 / Blazor** (ASP.NET Core, modo interactivo de servidor). La interfaz y la lógica están en C#. Solo hay un archivo JS pequeño para el tema, el arrastre en Firefox y la tecla Escape.
- **Sincronización en vivo**: cuando una persona guarda algo, la pantalla de la otra se actualiza sola.
- **Diseño** basado en el manual de marca 2026: azul `#0076e3`, azul oscuro `#1e222b`, gris `#c3cfdb`, acentos `#00d4a1` y `#00c9ff`, tipografías Poppins y Assistant, bordes de 5 px. Es minimalista, oscuro por defecto y con botón para pasar a modo claro (cada navegador recuerda su elección).
- **Acceso con usuario y clave** (cookie de sesión de 30 días).
- **Exportar a PDF**: informe con logo, resumen por fase, cronograma y lista de tareas con subtareas (A4 horizontal). Respeta los filtros activos.
- **Exportar a CSV** (se abre en Excel) y **descargar un respaldo .json**.

## ¿Necesita base de datos?

**No.** Los datos se guardan en un archivo JSON (`cronograma.json`). En Azure App Service la carpeta `/home` es persistente: el archivo sobrevive a reinicios y despliegues. La app lo guarda en `/home/data/` y deja una copia diaria de los últimos 30 días en `/home/data/respaldos/`. Para dos personas es suficiente y no cuesta nada extra.

La única condición es usar **una sola instancia** de la app. Es lo normal en el plan gratuito F1, que no permite escalar a varias.

> ¿Cuándo convendría una base de datos? Si en el futuro lo usan muchas personas, si necesitan varias instancias o si quieren reportes con SQL. Las opciones gratuitas serían la oferta gratuita de **Azure SQL Database** o el nivel gratuito de **Azure Cosmos DB**. Habría que cambiar `Dominio/Almacen.cs`; el resto de la app no cambia.

## Estructura

```
Program.cs                 Arranque, inicio de sesión, CSV y respaldo
Dominio/                   Modelo, almacenamiento JSON, fechas y festivos, configuración
Components/Pages/          Inicio (el planificador) e Ingresar (login)
Components/Planner/        Vistas: Lista, Tablero, Cronograma, Calendario, editor y ajustes
wwwroot/                   Estilos, logo y un JS mínimo
.github/workflows/azure.yml  Compila y despliega (main → producción, QA → pruebas)
```

## Ramas

| Rama | Uso |
|------|-----|
| `main` | Producción. Lo que se sube aquí se despliega a la app de producción. |
| `QA` | Pruebas. Lo que se sube aquí se despliega a la app de QA. |

Flujo sugerido: trabajar en una rama, abrir un PR hacia `QA`, probar y luego abrir un PR de `QA` a `main`.

## Probar en tu computador

Requiere el [SDK de .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet run
```

Abre http://localhost:5100. En local, si no configuras usuarios, entras sin clave. Para probar con usuarios:

```bash
USUARIOS="Sergio:clave1;Compañera:clave2" dotnet run
```

En Windows (PowerShell): `$env:USUARIOS="Sergio:clave1;Compañera:clave2"; dotnet run`

Los datos locales quedan en la carpeta `data/`, que está excluida de git.

## Publicar en Azure App Service (plan gratuito F1)

1. **Crear la app** en el portal de Azure: *App Services → Crear → Aplicación web*.
   - Publicar: **Código** · Pila del entorno de ejecución: **.NET 10 (LTS)** · Sistema operativo: **Linux** (o **Windows**, funciona igual).
   - Plan de precios: **Gratis F1**.
   - Para QA crea una segunda app (por ejemplo `cronograma-lg-qa`) en el **mismo plan F1**. El plan gratuito admite varias apps. Comparten la cuota diaria, pero cada una tiene sus propios datos.
2. **Configuración → Configuración general**: activa **Web sockets** y **Afinidad ARR**. Blazor los necesita.
3. **Configuración → Variables de entorno**: agrega
   | Nombre | Valor |
   |--------|-------|
   | `USUARIOS` | `Sergio:una-clave-segura;Nombre Dos:otra-clave-segura` |

   El formato es `Nombre:clave` separado por `;`. El nombre es el usuario con el que se ingresa (sin distinguir mayúsculas). Usa claves largas. Para cambiar una clave o quitar a alguien, edita la variable; las sesiones de quien ya no esté se cierran solas.
4. **Conectar GitHub** con el flujo incluido:
   - En cada app: *Información general → Descargar perfil de publicación*.
   - En GitHub, *Settings → Secrets and variables → Actions*:
     - **Secrets**: `AZURE_PUBLISH_PROFILE_PROD` y `AZURE_PUBLISH_PROFILE_QA`. Pega el contenido del archivo descargado.
     - **Variables**: `AZURE_WEBAPP_PROD` y `AZURE_WEBAPP_QA`. Pon el nombre de cada app.
   - Si al descargar el perfil Azure dice que está deshabilitado, activa *Configuración → Configuración general → Credenciales de publicación con autenticación básica de SCM*.
   - Cada push a `main` despliega producción y cada push a `QA` despliega pruebas.

   Otra opción es usar *Centro de implementación → GitHub* en Azure. En ese caso Azure crea su propio workflow y puedes borrar `.github/workflows/azure.yml`.

### Error «Operation cannot be completed without additional quota… F1 VMs: 0»

Azure asigna la cuota del plan F1 por suscripción, región y sistema operativo. Ese error quiere decir que tu suscripción tiene 0 planes F1 permitidos en la combinación que elegiste. La app funciona igual en Linux y en Windows, así que prueba en este orden:

1. **Cambia la región** al crear el plan (por ejemplo *East US 2*, *Central US*, *South Central US*, *West US 3*, *Brazil South* o *West Europe*). La app y su plan deben quedar en la misma región.
2. **Cambia el sistema operativo a Windows** (pila *.NET 10*). La cuota F1 de Windows es independiente de la de Linux. En Windows los datos quedan en `D:\home\data` y no hay que cambiar nada del código.
3. **Pide aumento de cuota**: *Cuotas → App Service* (o *Ayuda y soporte → Nueva solicitud → Límites de servicio y suscripción (cuotas) → App Service*), elige la región y solicita **F1 = 1** (o 2 si también crearás QA). Las suscripciones de prueba o de estudiante a veces no permiten esta solicitud.
4. Si nada de lo anterior funciona, el plan **B1** (pago, ~13 USD/mes) no tiene esta restricción y sirve sin cambios.

**Límites del plan gratuito:** 60 minutos de CPU al día (de sobra para dos personas). La app se duerme tras unos 20 minutos sin uso, así que la primera carga del día tarda unos segundos. No admite dominio propio con SSL, pero la dirección `https://<nombre>.azurewebsites.net` funciona con HTTPS.

## Cómo crear las tareas

1. Entra con tu usuario.
2. **Crea las fases**: el cronograma empieza vacío. Pulsa **Crear fases** o **Fases y equipo**, escribe los nombres (por ejemplo *Diagnóstico*, *Depuración*, *Validación*, *Cierre*), ordénalas con ↑ y pulsa **Guardar**. Ahí también ajustas los nombres del equipo, que se usan como responsables.
3. **Agrega una tarea** con **+ Nueva tarea** (arriba a la derecha) o con **+ Tarea** junto al nombre de una fase en la vista Lista.
   - **Tarea**: el nombre (obligatorio).
   - **Fase** y **Responsable**: el responsable sugerido eres tú.
   - **Inicio** y **Entrega**: por defecto hoy y dentro de dos días. La entrega no puede ser antes del inicio.
   - **Estado**: Pendiente, En curso, Bloqueada o Hecha.
   - **Prioridad**: Alta, Media o Baja.
   - **Entregable**: lo que debe quedar al final (por ejemplo *Archivo consolidado .xlsx*).
   - **Subtareas**: escribe y pulsa Enter para cada paso. El avance de la tarea se calcula con las subtareas marcadas.
   - **Notas**: criterios, hallazgos, enlaces.
   - Pulsa **Guardar**.
4. **Seguimiento**:
   - En **Lista**, la casilla de la izquierda marca la tarea como hecha.
   - En **Tablero**, arrastra la tarjeta a otra columna para cambiar el estado.
   - Haz clic en cualquier tarea, barra o evento para editarla. **Eliminar** pide confirmación.
   - Las tareas vencidas aparecen en rojo y las que vencen en 2 días o menos, en ámbar.
   - Los filtros de responsable, estado y búsqueda sirven para todas las vistas.
5. **Exportar PDF** abre el informe en otra pestaña con el diálogo de impresión: elige *Guardar como PDF* como destino. Arriba puedes marcar qué incluir (cronograma, lista de tareas, subtareas, notas). Si tienes filtros puestos (responsable, estado o búsqueda), el informe solo incluye esas tareas.
6. **Exportar CSV** descarga todas las tareas para Excel. En *Fases y equipo → Descargar respaldo* obtienes una copia completa en JSON.
