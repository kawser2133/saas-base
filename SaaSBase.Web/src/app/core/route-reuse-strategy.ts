import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, DetachedRouteHandle, RouteReuseStrategy } from '@angular/router';

@Injectable()
export class CustomRouteReuseStrategy implements RouteReuseStrategy {
  private readonly storedRoutes = new Map<string, DetachedRouteHandle>();

  shouldDetach(route: ActivatedRouteSnapshot): boolean {
    return this.isMainLayoutRoute(route);
  }

  store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
    if (handle && this.isMainLayoutRoute(route)) {
      const key = this.getRouteKey(route);
      this.storedRoutes.set(key, handle);
    }
  }

  shouldAttach(route: ActivatedRouteSnapshot): boolean {
    const key = this.getRouteKey(route);
    return this.storedRoutes.has(key) && this.isMainLayoutRoute(route);
  }

  retrieve(route: ActivatedRouteSnapshot): DetachedRouteHandle | null {
    const key = this.getRouteKey(route);
    return this.storedRoutes.get(key) || null;
  }

  shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    if (future.routeConfig === curr.routeConfig) {
      return true;
    }

    if (this.isMainLayoutRoute(future) && this.isMainLayoutRoute(curr)) {
      return true;
    }

    const futureParent = this.findMainLayoutParent(future);
    const currParent = this.findMainLayoutParent(curr);

    if (futureParent && currParent) {
      if (futureParent.routeConfig === currParent.routeConfig) {
        return true;
      }

      const futureParentPath = futureParent.routeConfig?.path || '';
      const currParentPath = currParent.routeConfig?.path || '';

      if (futureParentPath === currParentPath && this.isMainLayoutRoute(futureParent) && this.isMainLayoutRoute(currParent)) {
        return true;
      }
    }

    return false;
  }

  private findMainLayoutParent(route: ActivatedRouteSnapshot): ActivatedRouteSnapshot | null {
    let parent = route.parent;
    while (parent) {
      if (this.isMainLayoutRoute(parent)) {
        return parent;
      }
      parent = parent.parent;
    }
    return null;
  }

  private isMainLayoutRoute(route: ActivatedRouteSnapshot): boolean {
    if (route.routeConfig?.loadComponent) {
      const loadComponent = route.routeConfig.loadComponent.toString();
      return loadComponent.includes('main-layout.component');
    }
    return false;
  }

  private getRouteKey(route: ActivatedRouteSnapshot): string {
    const path = route.routeConfig?.path || '';
    const parentPath = route.parent?.routeConfig?.path || '';
    return `${parentPath}/${path}`;
  }
}
