import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileEditComponent } from './file-edit.component';
import { FileExplorerModule } from '../../file-explorer.module';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileEditComponent', () => {
  let component: FileEditComponent;
  let fixture: ComponentFixture<FileEditComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileExplorerModule, CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileEditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
