import { AfterViewInit, Directive, ElementRef, HostListener, OnDestroy, inject } from '@angular/core';

@Directive({ selector: '[appDrawer]' })
export class DrawerDirective implements AfterViewInit, OnDestroy {
    private element: ElementRef<HTMLElement> = inject(ElementRef);
    private previous: HTMLElement | null = null;
    private overflow = '';
    private controls() {
        return Array.from(
            this.element.nativeElement.querySelectorAll<HTMLElement>(
                'button:not(:disabled),a[href],input:not(:disabled),select:not(:disabled),summary,[tabindex="0"]'
            )
        ).filter((e) => e.getClientRects().length > 0);
    }
    ngAfterViewInit() {
        this.previous = document.activeElement as HTMLElement;
        this.overflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        this.controls()[0]?.focus();
    }
    ngOnDestroy() {
        document.body.style.overflow = this.overflow;
        if (this.previous?.isConnected) this.previous.focus();
    }
    @HostListener('keydown', ['$event']) trapFocus(event: KeyboardEvent) {
        if (event.key !== 'Tab') return;
        const controls = this.controls(),
            first = controls[0],
            last = controls[controls.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    }
}
