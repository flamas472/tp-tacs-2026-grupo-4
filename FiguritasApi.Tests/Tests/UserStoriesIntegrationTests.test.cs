using System.Net;
using System.Net.Http.Json;
using FiguritasApi.Model;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FiguritasApi.Tests;

/// <summary>
/// Pruebas de integración para las User Stories de la Entrega 1.
/// Utiliza WebApplicationFactory para levantar la API en memoria y testear el diseño REST.
/// </summary>
public class UserStoriesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public UserStoriesIntegrationTests (WebApplicationFactory<Program> factory)
    {
        // Se inicializa el cliente HTTP que apuntará a la aplicación en memoria
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        var usuario1 = new
        {
            id = 0,
            nombreUsuario = "string",
            figuritasRepetidas = new[]
            {
                new
                {
                    id = 0,
                    figurita = new
                    {
                        id = 0,
                        numero = 0,
                        seleccion = "Argentina",
                        equipo = "Boca",
                        categoria = "Prueba"
                    },
                    usuarioID = 0,
                    puedeIntercambiarse = true,
                    activo = true,
                    cantidad = 0
                }
            },
            figuritasFaltantes = new[]
            {
                new
                {
                    id = 0,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            },
        };
        var usuario2 = new
        {
            id = 1,
            nombreUsuario = "string",
            figuritasRepetidas = new[]
            {
                new
                {
                    id = 1,
                    figurita = new
                    {
                        id = 0,
                        numero = 0,
                        seleccion = "Argentina",
                        equipo = "Boca",
                        categoria = "Prueba"
                    },
                    usuarioID = 1,
                    puedeIntercambiarse = true,
                    activo = true,
                    cantidad = 0
                }
            },
            figuritasFaltantes = new[]
            {
                new
                {
                    id = 999,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            },
        };

        var response1 = await _client.PostAsJsonAsync($"/usuarios", usuario1);
        response1.EnsureSuccessStatusCode();

        var response2 = await _client.PostAsJsonAsync($"/usuarios", usuario2);
        response2.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task US01_PublicarFiguritaRepetida_DeberiaRetornarCreated()
    {
        // Arrange
        var usuarioId = 0;
        var nuevaRepetida = new
        {
            figurita = new
            {
                id = 0,
                numero = 34,
                seleccion = "0",
                equipo = "0",
                categoria = "0"
            },
            puedeIntercambiarse = true,
            activo = true
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/usuarios/{usuarioId}/Repetidas", nuevaRepetida);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        //Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task US02_RegistrarFiguritaFaltante_DeberiaRetornarCreated()
    {
        // Arrange
        var usuarioId = 0;
        var figuritaFaltante = new
        {
            figurita = new
            {
                id = 32,
                numero = 23,
                seleccion = "Argentina",
                equipo = "Boca",
                categoria = "Prueba01"
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/usuarios/{usuarioId}/faltantes", figuritaFaltante);
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine(content);
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);;

        var responseGet = await _client.GetAsync($"/usuarios/{usuarioId}/faltantes");
        responseGet.EnsureSuccessStatusCode();
        var contentGet = await responseGet.Content.ReadAsStringAsync();
        Console.WriteLine(contentGet);
        Assert.Contains("Prueba01", contentGet); 
    }

    [Fact]
    public async Task US03_BuscarFiguritasDisponibles_DeberiaRetornarListaFiltrada()
    {
        // Arrange
        await _client.PostAsync($"/FiguritasRepetidas", JsonContent.Create(new
        {
            id = 0,
            figurita = new
            {
                id = 0,
                numero = 0,
                seleccion = "Argentina",
                equipo = "Boca",
                categoria = "Prueba02"
            },
            usuarioID = 0,
            puedeIntercambiarse = true,
            activo = true,
            cantidad = 0
        }));

        var numeroABuscar = 0;

        var response = await _client.GetAsync($"/FiguritasRepetidas?numero={numeroABuscar}");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        //Console.WriteLine(content);
        Assert.Contains("Prueba02", content); // Asumiendo que el mock en memoria devuelve este dato
    }

    [Fact]
    public async Task US04_BuscarRecomendaciones_DeberiaRetornarListaFiltrada()
    {
        var idFiguritaFaltante = 566;

        var responseFaltante = await _client.PostAsync($"/Usuarios/{1}/faltantes", JsonContent.Create(
        new
        {
            figurita= new
            {
            id = idFiguritaFaltante,
            numero = 0,
            seleccion = "Brasil",
            equipo = "Boca",
            categoria = "Prueba03"
        }}));

        responseFaltante.EnsureSuccessStatusCode();

        var responseRepetidas =await _client.PostAsync($"/Usuarios/{0}/repetidas", JsonContent.Create(new
        {
            id = 0,
            figurita = new
            {
                id = idFiguritaFaltante,
                numero = 0,
                seleccion = "Brasil",
                equipo = "Boca",
                categoria = "Prueba03"
            },
            puedeIntercambiarse = true,
            activo = true,
            cantidad = 1
        }));

        responseRepetidas.EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"Usuarios/{1}/recomendaciones");
        response.EnsureSuccessStatusCode();

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        Console.WriteLine(content);
        Assert.Contains("Prueba03", content); // Asumiendo que el mock en memoria devuelve este dato
    }

    [Fact]
    public async Task US05_HacerPropuestaIntercambio_DeberiaRetornarCreated()
    {
        // Arrange
        var usuarioProponenteId = 0;
        var usuarioReceptorId = 0;
        var propuesta = new
        {
            UsuarioProponenteID = usuarioProponenteId,
            FiguritasOfrecidas = new[]
            {
                new
                {
                    id = 0,
                    figurita = new
                    {
                        id = 0,
                        numero = 0,
                        seleccion = "Argentina",
                        equipo = "Boca",
                        categoria = "Prueba"
                    },
                    usuarioID = 0,
                    puedeIntercambiarse = true,
                    activo = true,
                    cantidad = 0
                }
            },
            FiguritasARecibir = new[]
            {
                new
                {
                    id = 0,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            },
        };

        var response = await _client.PostAsJsonAsync($"/usuarios/{usuarioReceptorId}/intercambios", propuesta);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var responseGet = await _client.GetAsync($"/intercambios?UsuarioPropuestoID={usuarioReceptorId}");
        responseGet.EnsureSuccessStatusCode();

        Assert.NotEmpty(await responseGet.Content.ReadAsStringAsync()); // Verifica que la propuesta se haya registrado correctamente

    }

    [Fact]
    public async Task US06_PublicarFiguritaEnSubasta_DeberiaRetornarCreated()
    {
        // Arrange
        var figuritaRepetidaId = 150;
        var usuarioSubastador = new
        {
            id = 0,
            nombreUsuario = "string",
            figuritasRepetidas = new[]
            {
                new
                {
                    id = figuritaRepetidaId,
                    figurita = new
                    {
                        id = 0,
                        numero = 0,
                        seleccion = "Argentina",
                        equipo = "Boca",
                        categoria = "Prueba"
                    },
                    usuarioID = 0,
                    puedeIntercambiarse = true,
                    activo = true,
                    cantidad = 0
                }
            },
            figuritasFaltantes = Array.Empty<object>(),
        };
        var reglasSubasta = new
        {
            ID = 48,
            Subastador = usuarioSubastador,
            FechaInicio = DateTime.UtcNow,
            FechaFin = DateTime.UtcNow.AddDays(7),
            OfertaMinima = new[]
            {
                new
                {
                    id = 0,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            },
            FiguritasSubastadas = new[]
            {
                new
                {
                    id = 0,
                    numero = 0,
                    seleccion = "Argentina",
                    equipo = "Boca",
                    categoria = "Prueba"
                }
            },            
            Ofertas = Array.Empty<object>()
        };

        // Act - Mapeando a un recurso anidado en FiguritasRepetidasController
        var response = await _client.PostAsJsonAsync($"/subastas", reglasSubasta);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var getResponse = await _client.GetAsync($"/subastas?SubastadorId={usuarioSubastador.id}");
        getResponse.EnsureSuccessStatusCode();

        var content = await getResponse.Content.ReadAsStringAsync();
        Assert.NotEmpty(content); // Verifica que la subasta se haya registrado correctamente
    }

    [Fact]
    public async Task US07_OfertarEnSubastaActiva_DeberiaRetornarOk()
    {
        // Arrange
        var subastaId = 10;
        var oferta = new
        {
            UsuarioOfertanteId = 2,
            FiguritasOfrecidasIds = new[] { 300, 301 }
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/subastas/{subastaId}/ofertas", oferta);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task US08_VerTableroDeUsuario_DeberiaRetornarEstadoCompleto()
    {
        // Arrange
        var usuarioId = 1;

        // Act
        var response = await _client.GetAsync($"/api/usuarios/{usuarioId}/dashboard");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("publicadas", content);
        Assert.Contains("propuestas", content);
        Assert.Contains("subastasActivas", content);
    }

    [Fact]
    public async Task US09_AceptarPropuestaRecibida_DeberiaActualizarEstadoYRetornarOk()
    {
        // Arrange
        var usuarioId = 2; // El que recibe
        var propuestaId = 5;
        var cambioDeEstado = new { Estado = "Aceptada" }; // Podría mapear al enum EstadoPropuestaIntercambio

        // Act
        var response = await _client.PutAsJsonAsync($"/api/usuarios/{usuarioId}/propuestas/{propuestaId}/estado", cambioDeEstado);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
    }
}