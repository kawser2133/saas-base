import { Directive, Input, TemplateRef, ViewContainerRef, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { AuthorizationService } from '../services/authorization.service';

@Directive({
  selector: '[hasPermission]',
  standalone: true
})
export class HasPermissionDirective implements OnInit, OnDestroy {
  private hasPermissionValue: string | string[] | null = null;
  private mode: 'all' | 'any' = 'all';
  private subscription?: Subscription;
  private hasView = false;

  @Input()
  set hasPermission(value: string | string[] | null) {
    this.hasPermissionValue = value;
    this.updateView();
  }

  @Input()
  set hasPermissionMode(value: 'all' | 'any') {
    this.mode = value || 'all';
    this.updateView();
  }

  constructor(
    private templateRef: TemplateRef<any>,
    private viewContainer: ViewContainerRef,
    private authorizationService: AuthorizationService
  ) {}

  ngOnInit(): void {
    // Load permissions on init if not already loaded
    const userId = localStorage.getItem('userId');
    if (userId) {
      this.subscription = this.authorizationService.loadUserPermissionCodes(userId).subscribe(() => {
        this.updateView();
      });
    }
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  private updateView(): void {
    if (!this.hasPermissionValue) {
      this.clearView();
      return;
    }

    const permissions = Array.isArray(this.hasPermissionValue)
      ? this.hasPermissionValue
      : [this.hasPermissionValue];

    const hasAccess = this.checkPermissions(permissions);

    if (hasAccess && !this.hasView) {
      this.viewContainer.createEmbeddedView(this.templateRef);
      this.hasView = true;
    } else if (!hasAccess && this.hasView) {
      this.clearView();
    }
  }

  private checkPermissions(permissions: string[]): boolean {
    if (permissions.length === 0) return true;

    // Admin bypass
    const roles = (localStorage.getItem('roles') || '').split(',').filter(Boolean);
    if (roles.includes('System Administrator')) return true;

    if (this.mode === 'any') {
      // User needs ANY of the permissions
      return permissions.some(code => this.authorizationService.hasPermission(code));
    } else {
      // User needs ALL permissions
      return this.authorizationService.requirePermissions(permissions);
    }
  }

  private clearView(): void {
    this.viewContainer.clear();
    this.hasView = false;
  }
}

