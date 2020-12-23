using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using WebApplication.Services;
using WebApplication.Framework;
using WebApplication.Models;

namespace WebApplication.Controllers
{
    public partial class TaskTypeController : BaseController
    {
        protected readonly AdsGoFastContext _context;
        

        public TaskTypeController(AdsGoFastContext context, ISecurityAccessProvider securityAccessProvider, IEntityRoleProvider roleProvider) : base(securityAccessProvider, roleProvider)
        {
            Name = "TaskType";
            _context = context;
        }

        // GET: TaskType
        public async Task<IActionResult> Index()
        {
            return View(await _context.TaskType.ToListAsync());
        }

        // GET: TaskType/Details/5
        [ChecksUserAccess]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var taskType = await _context.TaskType
                .FirstOrDefaultAsync(m => m.TaskTypeId == id);
            if (taskType == null)
                return NotFound();
            if (!await CanPerformCurrentActionOnRecord(taskType))
                return new ForbidResult();


            return View(taskType);
        }

        // GET: TaskType/Create
        public IActionResult Create()
        {
     TaskType taskType = new TaskType();
            taskType.ActiveYn = true;
            return View(taskType);
        }

        // POST: TaskType/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ChecksUserAccess]
        public async Task<IActionResult> Create([Bind("TaskExecutionType,TaskTypeId,TaskTypeName,TaskTypeJson,ActiveYn")] TaskType taskType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(taskType);
                if (!await CanPerformCurrentActionOnRecord(taskType))
                {
                    return new ForbidResult();
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(IndexDataTable));
            }
            return View(taskType);
        }

        // GET: TaskType/Edit/5
        [ChecksUserAccess()]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var taskType = await _context.TaskType.FindAsync(id);
            if (taskType == null)
                return NotFound();

            if (!await CanPerformCurrentActionOnRecord(taskType))
                return new ForbidResult();
            return View(taskType);
        }

        // POST: TaskType/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ChecksUserAccess]
        public async Task<IActionResult> Edit(int id, [Bind("TaskExecutionType,TaskTypeId,TaskTypeName,TaskTypeJson,ActiveYn")] TaskType taskType)
        {
            if (id != taskType.TaskTypeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(taskType);

                    if (!await CanPerformCurrentActionOnRecord(taskType))
                        return new ForbidResult();
			
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaskTypeExists(taskType.TaskTypeId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(IndexDataTable));
            }
            return View(taskType);
        }

        // GET: TaskType/Delete/5
        [ChecksUserAccess]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var taskType = await _context.TaskType
                .FirstOrDefaultAsync(m => m.TaskTypeId == id);
            if (taskType == null)
                return NotFound();
		
            if (!await CanPerformCurrentActionOnRecord(taskType))
                return new ForbidResult();

            return View(taskType);
        }

        // POST: TaskType/Delete/5
        [HttpPost, ActionName("Delete")]
        [ChecksUserAccess()]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var taskType = await _context.TaskType.FindAsync(id);

            if (!await CanPerformCurrentActionOnRecord(taskType))
                return new ForbidResult();
		
            _context.TaskType.Remove(taskType);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(IndexDataTable));
        }

        private bool TaskTypeExists(int id)
        {
            return _context.TaskType.Any(e => e.TaskTypeId == id);
        }
    }
}
