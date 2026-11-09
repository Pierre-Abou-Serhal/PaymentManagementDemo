import { Component, Input } from '@angular/core';
import { IconComponent } from '../../../layout/component/app.icon';
import { GroupHeader } from '../model/payment';

@Component({
    selector: 'app-group-header',
    imports: [IconComponent],
    templateUrl: './group-header.component.html'
})
export class GroupHeaderComponent {
    @Input() header: GroupHeader | null | undefined = null;
    @Input() compact = false;
}
