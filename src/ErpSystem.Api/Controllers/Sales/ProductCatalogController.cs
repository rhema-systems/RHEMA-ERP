using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Sales;

[Authorize]
[ApiController]
[Route("api/sales/products")]
public class ProductCatalogController : ControllerBase
{
    private readonly IProductCatalogService _productService;

    public ProductCatalogController(IProductCatalogService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? productType = null,
        [FromQuery] bool? isActive = null)
    {
        var result = await _productService.GetAllAsync(page, pageSize, search, category, productType, isActive);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _productService.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var product = await _productService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        var product = await _productService.UpdateAsync(id, dto);
        return Ok(product);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var product = await _productService.ActivateAsync(id);
        return Ok(product);
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var product = await _productService.DeactivateAsync(id);
        return Ok(product);
    }

    [HttpGet("by-category/{category}")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        var products = await _productService.GetByCategoryAsync(category);
        return Ok(products);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int maxResults = 20)
    {
        var products = await _productService.SearchAsync(query, maxResults);
        return Ok(products);
    }
}
