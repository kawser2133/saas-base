import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BreadcrumbService, BreadcrumbItem } from '../../../core/services/breadcrumb.service';

@Component({
  selector: 'app-breadcrumb',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <nav class="breadcrumbs" aria-label="Breadcrumb">
      <ng-container *ngFor="let crumb of breadcrumbs; let last = last; let i = index">
        <!-- Non-clickable parent (no route) -->
        <span *ngIf="!last && !crumb.url" class="crumb">
          <i *ngIf="crumb.icon" [class]="'fas ' + crumb.icon"></i>
          <span>{{ crumb.label }}</span>
        </span>
        <!-- Clickable breadcrumb -->
        <a 
          *ngIf="!last && crumb.url" 
          [routerLink]="crumb.url" 
          class="crumb crumb-link">
          <i *ngIf="crumb.icon" [class]="'fas ' + crumb.icon"></i>
          <span>{{ crumb.label }}</span>
        </a>
        <!-- Current page (last item) -->
        <span *ngIf="last" class="crumb current">
          <i *ngIf="crumb.icon" [class]="'fas ' + crumb.icon"></i>
          <span>{{ crumb.label }}</span>
        </span>
        <!-- Separator -->
        <i *ngIf="!last" class="fas fa-angle-right separator" aria-hidden="true"></i>
      </ng-container>
    </nav>
  `,
  styles: []
})
export class BreadcrumbComponent implements OnInit {
  breadcrumbs: BreadcrumbItem[] = [];

  constructor(private breadcrumbService: BreadcrumbService) {}

  ngOnInit(): void {
    this.breadcrumbService.breadcrumbs$.subscribe(breadcrumbs => {
      this.breadcrumbs = breadcrumbs;
    });
  }
}

